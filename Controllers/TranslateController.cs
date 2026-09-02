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

    private static readonly string[] TranslateModels = { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-flash-latest" };
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "text", "name" };
    private static readonly Dictionary<string, string> TargetLangNames = new()
    {
        ["ru"] = "Russian (русский)", ["en"] = "English", ["fr"] = "French (français)",
        ["el"] = "Greek (ελληνικά)", ["ar"] = "Arabic (العربية)"
    };

    public TranslateController(IHttpClientFactory httpFactory, IConfiguration config, IMemoryCache cache, ILogger<TranslateController> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public class TranslateRequest
    {
        public string Text { get; set; } = "";
        public string TargetLang { get; set; } = "en";
        public string Kind { get; set; } = "text";
    }

    [HttpPost]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest req)
    {
        // This endpoint must stay available to guests because public doctor/review
        // pages use it. Protect it from cross-site API-key abuse by accepting only
        // same-origin browser calls (non-browser tests/local tools may omit Origin).
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
                        var response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == 0) { await Task.Delay(400); continue; }
                            break;
                        }
                        if ((int)response.StatusCode == 404) break;
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Translation provider returned {Status} for {Model}", (int)response.StatusCode, model);
                            return req.Text;
                        }

                        var raw = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(raw);
                        var result = doc.RootElement.GetProperty("candidates")[0]
                            .GetProperty("content").GetProperty("parts")[0]
                            .GetProperty("text").GetString()?.Trim() ?? req.Text;
                        _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Translation request failed for {Model}", model);
                        break;
                    }
                }
            }
            return req.Text;
        });

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
        if (string.IsNullOrWhiteSpace(origin)) return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)) return false;
        return string.Equals(originUri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(originUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && originUri.Port == (Request.Host.Port ?? (Request.IsHttps ? 443 : 80));
    }
}
