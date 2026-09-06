using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DentalClinic.Models;
using DentalClinic.Data;
using DentalClinic.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notifications;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReviewController> _logger;
    private readonly IWebHostEnvironment _environment;

    // Тот же список моделей Gemini, что и в ChatController — от самой дешёвой
    // (для короткой задачи перевода этого достаточно) к более умной как фолбэк.
    // См. комментарий в TranslateController.cs — "gemini-flash-latest" как
    // последний, самый надёжный вариант, если конкретные версии моделей 404.
    private static readonly string[] TranslateModels = { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-flash-latest" };

    private static readonly Dictionary<string, string> TargetLangNames = new()
    {
        ["en"] = "English",
        ["fr"] = "French (français)",
        ["el"] = "Greek (ελληνικά)",
        ["ar"] = "Arabic (العربية)"
    };

    public ReviewController(
        ApplicationDbContext context,
        NotificationService notifications,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<ReviewController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _notifications = notifications;
        _httpFactory = httpFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
        _environment = environment;
    }

    // =========================
    // Перевод текста отзыва на выбранный пользователем язык интерфейса.
    // Клиент передаёт только идентификатор отзыва и целевой язык. Исходный текст
    // всегда загружается сервером из БД, иначе публичный endpoint можно было бы
    // использовать как бесплатный прокси для перевода произвольных 1000 символов,
    // тратя квоту Gemini клиники.
    // =========================
    public sealed class TranslateReviewRequest
    {
        public int ReviewId { get; set; }
        public string TargetLang { get; set; } = "en";
    }

    [HttpPost("translate")]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> TranslateReview([FromBody] TranslateReviewRequest req)
    {
        // Endpoint нужен и гостям для публичной карусели. В production разрешаем
        // только same-origin browser requests, чтобы сторонний сайт не мог расходовать
        // квоту перевода клиники напрямую из браузеров своих посетителей.
        if (!IsAllowedOrigin())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cross-origin review translation is not allowed" });

        if (req.ReviewId <= 0)
            return BadRequest(new { message = "Invalid review id" });

        var lang = (req.TargetLang ?? "").Trim().ToLowerInvariant();
        if (lang != "ru" && !TargetLangNames.TryGetValue(lang, out _))
            return BadRequest(new { message = "Unsupported language" });

        var review = await _context.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == req.ReviewId);

        if (review == null)
            return NotFound();

        // Одобренные отзывы публичны. Pending/rejected доступны для перевода только
        // самому автору или администратору — те же правила, что и для списка отзывов
        // пациента. Это не даёт перебором ID читать приватный текст модерации.
        if (!string.Equals(review.Status, "approved", StringComparison.OrdinalIgnoreCase)
            && !IsOwnerOrAdmin(review.PatientId))
        {
            return Forbid();
        }

        var originalText = review.Text ?? string.Empty;

        // Русский — язык оригинала отзывов по умолчанию; AI для него не нужен.
        if (lang == "ru" || string.IsNullOrWhiteSpace(originalText))
            return Ok(new { text = originalText });

        var langName = TargetLangNames[lang];
        var safeText = originalText.Length > 1000 ? originalText[..1000] : originalText;
        var cacheKey = BuildReviewTranslationCacheKey(review.Id, lang, safeText);
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return Ok(new { text = cached });

        if (string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]))
            return Ok(new { text = originalText });

        var systemPrompt =
            $"Translate the user's text to {langName}. " +
            "Reply with ONLY the translated text, no quotes, no explanations, no extra formatting. " +
            "If the text is already in the target language, return it unchanged.";

        var body = JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = safeText } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 500 }
        });

        var http = _httpFactory.CreateClient();

        // Тот же общий "шлагбаум" на запросы к Gemini, что и в TranslateController —
        // без него отзывы и имена/комментарии конкурируют за одну и ту же
        // маленькую квоту API одновременно и чаще ловят 429.
        var translated = await GeminiTranslateLimiter.RunAsync(async () =>
        {
            foreach (var model in TranslateModels)
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        // Реальный ключ не кладём в URL. Общий GeminiApiKeyHandler
                        // заменяет compatibility key заголовком x-goog-api-key.
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key=compat";
                        var response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == 0) { await Task.Delay(400); continue; }
                            break;
                        }
                        if ((int)response.StatusCode == 404)
                            break;

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Ошибка перевода отзыва ({Status}) для модели {Model}", (int)response.StatusCode, model);
                            return originalText;
                        }

                        var raw = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(raw);
                        var result = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString()?.Trim() ?? originalText;

                        _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось перевести отзыв (модель {Model})", model);
                        break;
                    }
                }
            }

            // Все модели недоступны/перегружены — отдаём оригинал, чтобы страница не сломалась.
            return originalText;
        });

        return Ok(new { text = translated });
    }

    internal static string BuildReviewTranslationCacheKey(int reviewId, string lang, string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"review\n{reviewId}\n{lang}\n{text}"));
        return $"review-translate:{Convert.ToHexString(bytes)}";
    }

    private bool IsAllowedOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            var fetchSite = Request.Headers["Sec-Fetch-Site"].ToString();
            if (string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase)) return true;
            return _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)) return false;
        return string.Equals(originUri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(originUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && originUri.Port == (Request.Host.Port ?? (Request.IsHttps ? 443 : 80));
    }

    // =========================
    // ПУБЛИЧНО: одобренные отзывы для карусели на главной + средний рейтинг
    // =========================
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved()
    {
        var reviews = await _context.Reviews
            .Where(r => r.Status == "approved")
            .OrderByDescending(r => r.ModeratedAt)
            .Join(_context.Patients,
                r => r.PatientId,
                p => p.Id,
                (r, p) => new
                {
                    r.Id,
                    r.Rating,
                    r.Text,
                    r.CreatedAt,
                    PatientName = p.FirstName
                })
            .ToListAsync();

        var average = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

        return Ok(new
        {
            average,
            count = reviews.Count,
            reviews
        });
    }

    // =========================
    // Отзывы конкретного пациента (для личного кабинета, включая статус и причину отклонения)
    // Только сам пациент (по токену) или админ.
    // =========================
    [HttpGet("patient/{patientId:int}")]
    [Authorize]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        if (!IsOwnerOrAdmin(patientId)) return Forbid();

        var reviews = await _context.Reviews
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(reviews);
    }

    // =========================
    // Отметить уведомление об отклонении как прочитанное (только владелец отзыва)
    // =========================
    [HttpPost("{id:int}/mark-read")]
    [Authorize]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        if (!IsOwnerOrAdmin(review.PatientId)) return Forbid();

        review.IsNotificationRead = true;
        await _context.SaveChangesAsync();

        return Ok(new { review.Id, review.IsNotificationRead });
    }

    // =========================
    // Оставить отзыв (только зарегистрированный пациент).
    // PatientId больше не берётся из тела запроса — только из проверенного токена,
    // иначе можно было отправить отзыв от имени чужого пациента.
    // =========================
    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req)
    {
        var patientId = GetCurrentUserId();

        if (req.Rating < 1 || req.Rating > 5)
        {
            return BadRequest(new { message = "❌ Оценка должна быть от 1 до 5" });
        }

        if (string.IsNullOrWhiteSpace(req.Text) || req.Text.Trim().Length < 10)
        {
            return BadRequest(new { message = "❌ Текст отзыва должен содержать не менее 10 символов" });
        }

        var review = new Review
        {
            PatientId = patientId,
            Rating = req.Rating,
            Text = req.Text.Trim(),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        // Realtime: у всех открытых вкладок админки живой список "на проверке" обновится сам
        await _notifications.NotifyAdminsAsync(
            "new_review",
            $"Новый отзыв на модерации (оценка {review.Rating}★)",
            review.Id);

        return Ok(new
        {
            id = review.Id,
            message = "✅ Спасибо! Ваш отзыв отправлен на проверку модератору"
        });
    }

    // =========================
    // АДМИН: список отзывов на проверке
    // =========================
    [HttpGet("admin/pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending()
        => Ok(await _AdminQuery("pending"));

    // =========================
    // АДМИН: одобренные отзывы
    // =========================
    [HttpGet("admin/approved")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetApprovedAdmin()
        => Ok(await _AdminQuery("approved"));

    // =========================
    // АДМИН: отклонённые отзывы
    // =========================
    [HttpGet("admin/rejected")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRejectedAdmin()
        => Ok(await _AdminQuery("rejected"));

    private async Task<object> _AdminQuery(string status)
    {
        return await _context.Reviews
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Join(_context.Patients,
                r => r.PatientId,
                p => p.Id,
                (r, p) => new
                {
                    r.Id,
                    r.PatientId,
                    PatientName = p.FirstName,
                    PatientEmail = p.Email,
                    r.Rating,
                    r.Text,
                    r.Status,
                    r.RejectionReason,
                    r.CreatedAt,
                    r.ModeratedAt
                })
            .ToListAsync();
    }

    // =========================
    // АДМИН: одобрить / отклонить отзыв
    // =========================
    [HttpPut("admin/{id:int}/moderate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Moderate(int id, [FromBody] ModerateReviewRequest dto)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        var status = dto.Status?.Trim().ToLower();

        if (status != "approved" && status != "rejected")
        {
            return BadRequest(new { message = "❌ Статус должен быть 'approved' или 'rejected'" });
        }

        if (status == "rejected" && string.IsNullOrWhiteSpace(dto.RejectionReason))
        {
            return BadRequest(new { message = "❌ Укажите причину отклонения отзыва" });
        }

        review.Status = status;
        review.RejectionReason = status == "rejected" ? dto.RejectionReason!.Trim() : null;
        review.ModeratedAt = DateTime.UtcNow;
        review.IsNotificationRead = false; // новое уведомление для пациента

        await _context.SaveChangesAsync();

        var message = status == "approved"
            ? "Ваш отзыв одобрен и опубликован на сайте 🎉"
            : $"Ваш отзыв отклонён. Причина: {review.RejectionReason}";

        await _notifications.NotifyAsync(
            review.PatientId,
            status == "approved" ? "review_approved" : "review_rejected",
            message,
            review.Id);

        return Ok(new { review.Id, review.Status, review.RejectionReason });
    }

    // =========================
    // Вспомогательные методы для проверки прав
    // =========================
    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsOwnerOrAdmin(int patientId)
    {
        if (User.IsInRole("Admin")) return true;
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) && id == patientId;
    }
}