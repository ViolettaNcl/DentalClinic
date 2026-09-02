using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace DentalClinic.Controllers;

/// <summary>
/// Same-origin translation endpoint for short UI/database text. The Gemini key never
/// leaves the server and cache keys are stable SHA-256 digests rather than GetHashCode.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class TranslateController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TranslateController> _logger;

    private static readonly string[] TranslateModels =
        ["gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-flash-latest"];

    private static readonly IReadOnlyDictionary<string, string> TargetLangNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ru"] = "Russian (русский)",
            ["en"] = "English",
            ["fr"] = "French (français)",
            ["el"] = "Greek (ελληνικά)",
            ["ar"] = "Arabic (العربية)"
        };

    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.OrdinalIgnoreCase) { "text", "name", "review", "comment" };

    public TranslateController(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<TranslateController> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public sealed class TranslateRequest
    {
        public string Text { get; set; } = string.Empty;
        public string TargetLang { get; set; } = "en";
        public string Kind { get; set; } = "text";
    }

    [HttpPost]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest req, CancellationToken cancellationToken)
    {
        if (!IsTrustedCaller())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Translation endpoint accepts same-origin browser requests only" });

        if (string.IsNullOrWhiteSpace(req.Text))
            return Ok(new { text = req.Text });

        var lang = (req.TargetLang ?? string.Empty).Trim().ToLowerInvariant();
        if (!TargetLangNames.TryGetValue(lang, out var langName))
            return BadRequest(new { message = "Unsupported target language" });

        var kind = AllowedKinds.Contains(req.Kind ?? string.Empty)
            ? req.Kind.Trim().ToLowerInvariant()
            : "text";

        var safeText = req.Text.Trim();
        if (safeText.Length > 1500)
            safeText = safeText[..1500];

        var cacheKey = CreateStableCacheKey(kind, lang, safeText);
        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
            return Ok(new { text = cached });

        if (string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]))
            return Ok(new { text = safeText });

        var systemPrompt = kind == "name"
            ? $"Transliterate/render this person's name naturally in {langName}. Reply only with the name, without quotes or explanation."
            : $"Translate the following text to {langName}. Reply only with the translated text, without quotes, explanations or extra formatting. If already in the target language, return it unchanged.";

        var body = JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = safeText } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 600 }
        });

        var http = _httpFactory.CreateClient();
        var translated = await GeminiTranslateLimiter.RunAsync(async () =>
        {
            foreach (var model in TranslateModels)
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        // GeminiApiKeyHandler adds x-goog-api-key. Never put a secret in URL/logs.
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
                        using var response = await http.PostAsync(
                            url,
                            new StringContent(body, Encoding.UTF8, "application/json"),
                            cancellationToken);

                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == 0)
                            {
                                await Task.Delay(400, cancellationToken);
                                continue;
                            }
                            break;
                        }

                        if ((int)response.StatusCode == 404)
                            break;

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Translation provider returned {Status} for {Model}", (int)response.StatusCode, model);
                            return safeText;
                        }

                        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                        var result = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString()?.Trim();

                        if (string.IsNullOrWhiteSpace(result))
                            return safeText;

                        _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                        return result;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Translation failed for model {Model}", model);
                        break;
                    }
                }
            }

            return safeText;
        });

        return Ok(new { text = translated });
    }

    private bool IsTrustedCaller()
    {
        if (User.Identity?.IsAuthenticated == true)
            return true;

        var expectedOrigin = $"{Request.Scheme}://{Request.Host}";

        var origin = Request.Headers.Origin.ToString();
        if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && string.Equals(originUri.GetLeftPart(UriPartial.Authority), expectedOrigin, StringComparison.OrdinalIgnoreCase))
            return true;

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            && string.Equals(refererUri.GetLeftPart(UriPartial.Authority), expectedOrigin, StringComparison.OrdinalIgnoreCase))
            return true;

        var fetchSite = Request.Headers["Sec-Fetch-Site"].ToString();
        return fetchSite.Equals("same-origin", StringComparison.OrdinalIgnoreCase)
            || fetchSite.Equals("same-site", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateStableCacheKey(string kind, string lang, string text)
    {
        var payload = Encoding.UTF8.GetBytes($"v3\n{kind}\n{lang}\n{text}");
        return $"translate:{Convert.ToHexString(SHA256.HashData(payload))}";
    }
}
