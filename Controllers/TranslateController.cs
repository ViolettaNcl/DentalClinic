using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslateController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TranslateController> _logger;
    private readonly IWebHostEnvironment _environment;

    private static readonly string[] TranslateModels = { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-flash-latest" };
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "text", "name" };
    private static readonly Dictionary<string, string> TargetLangNames = new()
    {
        ["ru"] = "Russian (русский)", ["en"] = "English", ["fr"] = "French (français)",
        ["el"] = "Greek (ελληνικά)", ["ar"] = "Arabic (العربية)"
    };

    public TranslateController(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<TranslateController> logger,
        IWebHostEnvironment environment)
    {
        _httpFactory = httpFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
        _environment = environment;
    }

    public class TranslateRequest
    {
        public string Text { get; set; } = "";
        public string TargetLang { get; set; } = "en";
        public string Kind { get; set; } = "text";
    }

    [HttpPost]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> Translate(
        [FromBody] TranslateRequest req,
        CancellationToken cancellationToken = default)
    {
        // The endpoint stays available to guests because public doctor/review pages
        // use it. In production, only same-origin browser requests are accepted so
        // third-party scripts cannot spend the clinic's Gemini quota by omitting Origin.
        if (!IsAllowedOrigin())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cross-origin translation is not allowed" });

        var lang = (req.TargetLang ?? "").Trim().ToLowerInvariant();
        var kind = (req.Kind ?? "text").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(req.Text)) return Ok(new { text = req.Text });
        if (!TargetLangNames.TryGetValue(lang, out var langName)) return BadRequest(new { message = "Unsupported language" });
        if (!AllowedKinds.Contains(kind)) return BadRequest(new { message = "Unsupported translation kind" });

        var safeText = req.Text.Length > 1500 ? req.Text[..1500] : req.Text;
        var cacheKey = BuildStableCacheKey(kind, lang, safeText);
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return Ok(new { text = cached });

        if (string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]))
            return Ok(new { text = req.Text });

        var systemPrompt = kind == "name"
            ? $"Transliterate/render this person's name naturally in {langName}. Reply with ONLY the name."
            : $"Translate the following text to {langName}. Reply with ONLY the translated text. If already in the target language, return it unchanged.";

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
                        // GeminiApiKeyHandler removes this compatibility query key
                        // and sends the real secret in x-goog-api-key.
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key=compat";
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
                        if ((int)response.StatusCode == 404) break;
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Translation provider returned {Status} for {Model}", (int)response.StatusCode, model);
                            return req.Text;
                        }

                        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                        using var doc = JsonDocument.Parse(raw);
                        var result = doc.RootElement.GetProperty("candidates")[0]
                            .GetProperty("content").GetProperty("parts")[0]
                            .GetProperty("text").GetString()?.Trim() ?? req.Text;
                        _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                        return result;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Translation request failed for {Model}", model);
                        break;
                    }
                }
            }
            return req.Text;
        }, cancellationToken);

        return Ok(new { text = translated });
    }

    internal static string BuildStableCacheKey(string kind, string lang, string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}\n{lang}\n{text}"));
        return $"translate:{Convert.ToHexString(bytes)}";
    }

    private bool IsAllowedOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            // Browser same-origin fetches can be identified even when a user agent
            // omits Origin. Direct tools/curl without browser metadata are allowed
            // only in local/test environments.
            var fetchSite = Request.Headers["Sec-Fetch-Site"].ToString();
            if (string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase)) return true;
            return _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)) return false;
        return string.Equals(originUri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(originUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && originUri.Port == (Request.Host.Port ?? (Request.IsHttps ? 443 : 80));
    }
}
