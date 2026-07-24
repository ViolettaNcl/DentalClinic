using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

/// <summary>
/// Общий эндпоинт перевода коротких кусков текста "на лету" — используется
/// для всего, что хранится в БД на языке автора и не переведено заранее:
/// имя врача, комментарий к записи и т.п. (для отзывов есть свой похожий
/// эндпоинт в ReviewController — тут та же логика, но без привязки к ID отзыва).
/// Сам исходный текст в базе никогда не меняется — перевод только для показа.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TranslateController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TranslateController> _logger;

    // Пробуем несколько вариантов моделей по очереди. "gemini-flash-latest" —
    // это алиас от Google, который сам всегда указывает на актуальную рабочую
    // версию Flash (сейчас это Gemini 3.5 Flash) — если конкретные версии вроде
    // gemini-2.5-flash вернут 404 (модель устарела/недоступна для этого проекта),
    // этот алиас должен сработать в любом случае.
    private static readonly string[] TranslateModels = { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-flash-latest" };

    private static readonly Dictionary<string, string> TargetLangNames = new()
    {
        ["ru"] = "Russian (русский)",
        ["en"] = "English",
        ["fr"] = "French (français)",
        ["el"] = "Greek (ελληνικά)",
        ["ar"] = "Arabic (العربية)"
    };

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

    public class TranslateRequest
    {
        public string Text { get; set; } = "";
        public string TargetLang { get; set; } = "en";
        // Необязательная подсказка модели о типе текста — помогает переводить
        // короче и точнее (например, имя человека переводится/транслитерируется
        // иначе, чем обычное предложение).
        public string Kind { get; set; } = "text"; // "text" | "name"
    }

    [HttpPost]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest req)
    {
        var lang = (req.TargetLang ?? "").ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(req.Text))
            return Ok(new { text = req.Text });

        if (!TargetLangNames.TryGetValue(lang, out var langName))
            return Ok(new { text = req.Text });

        var safeText = req.Text.Length > 1500 ? req.Text[..1500] : req.Text;
        var cacheKey = $"translate:{req.Kind}:{lang}:{safeText.GetHashCode()}";

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return Ok(new { text = cached });

        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return Ok(new { text = req.Text });

        var systemPrompt = req.Kind == "name"
            ? $"Transliterate/render the following person's name in {langName}, the way it would naturally appear to a {langName}-speaking reader (use the standard {langName} script/spelling conventions for foreign names). Reply with ONLY the name, nothing else — no quotes, no explanations."
            : $"Translate the following text to {langName}. Reply with ONLY the translated text, no quotes, no explanations, no extra formatting. If it's already in the target language, return it unchanged.";

        var body = JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = safeText } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 600 }
        });

        var http = _httpFactory.CreateClient();

        // ВАЖНО: раньше запрос к Gemini уходил напрямую, без прохождения через
        // GeminiTranslateLimiter — тот класс существовал в проекте, но нигде не
        // вызывался. Именно это, судя по всему, и было причиной постоянных 429:
        // при одновременной отрисовке таблицы (перевод сразу нескольких имён и
        // комментариев) в Gemini улетало сразу много запросов без какой-либо
        // паузы между ними, а квота ключа — маленькая. Теперь все обращения к
        // Gemini идут строго по одному, с минимальным интервалом между ними
        // (см. GeminiTranslateLimiter.cs) — это не увеличивает дневную квоту,
        // но резко снижает число отказов из-за одновременных запросов.
        var translated = await GeminiTranslateLimiter.RunAsync(async () =>
        {
            foreach (var model in TranslateModels)
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                        var response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

                        if ((int)response.StatusCode == 429)
                        {
                            // Кратковременная перегрузка — скорее всего несколько переводов
                            // запустились одновременно. Один раз подождём немного и попробуем
                            // снова тем же вариантом модели, прежде чем переходить к следующей.
                            if (attempt == 0) { await Task.Delay(400); continue; }
                            break; // переходим к следующей модели из списка
                        }
                        if ((int)response.StatusCode == 404)
                            break;

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Ошибка перевода текста ({Status}) для модели {Model}", (int)response.StatusCode, model);
                            return req.Text;
                        }

                        var raw = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(raw);
                        var result = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString()?.Trim() ?? req.Text;

                        _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось перевести текст (модель {Model})", model);
                        break;
                    }
                }
            }

            return req.Text;
        });

        return Ok(new { text = translated });
    }
}