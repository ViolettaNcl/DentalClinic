using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using DentalClinic.Models;
using DentalClinic.Data;
using DentalClinic.Services;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private const int PublicReviewLimit = 100;
    private const int PatientReviewCompatibilityLimit = 200;
    private const int AdminReviewCompatibilityLimit = 200;
    private const int DefaultAdminPageSize = 15;
    private const int MaxAdminPageSize = 100;

    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notifications;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReviewController> _logger;
    private readonly IWebHostEnvironment _environment;

    private static readonly SemaphoreSlim ProcessModerationGate = new(1, 1);
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

    public sealed class TranslateReviewRequest
    {
        public int ReviewId { get; set; }
        public string TargetLang { get; set; } = "en";
    }

    [HttpPost("translate")]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> TranslateReview(
        [FromBody] TranslateReviewRequest req,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedOrigin())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cross-origin review translation is not allowed" });

        if (req.ReviewId <= 0)
            return BadRequest(new { message = "Invalid review id" });

        var lang = (req.TargetLang ?? "").Trim().ToLowerInvariant();
        if (lang != "ru" && !TargetLangNames.TryGetValue(lang, out _))
            return BadRequest(new { message = "Unsupported language" });

        var review = await _context.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == req.ReviewId, cancellationToken);

        if (review == null)
            return NotFound();

        if (!string.Equals(review.Status, "approved", StringComparison.OrdinalIgnoreCase)
            && !IsOwnerOrAdmin(review.PatientId))
        {
            return Forbid();
        }

        var originalText = review.Text ?? string.Empty;

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
        var translated = await GeminiTranslateLimiter.RunAsync(async () =>
        {
            foreach (var model in TranslateModels)
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key=compat";
                        using var content = new StringContent(body, Encoding.UTF8, "application/json");
                        using var response = await http.PostAsync(url, content, cancellationToken);

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
                            _logger.LogWarning("Ошибка перевода отзыва ({Status}) для модели {Model}", (int)response.StatusCode, model);
                            return originalText;
                        }

                        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
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
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось перевести отзыв (модель {Model})", model);
                        break;
                    }
                }
            }

            return originalText;
        }, cancellationToken);

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

    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(CancellationToken cancellationToken)
    {
        // Keep the public carousel response bounded as review history grows. Aggregate
        // count/rating still represent the complete approved set, while only the most
        // recently moderated cards are materialized and sent to every anonymous visitor.
        var approved = _context.Reviews
            .AsNoTracking()
            .Where(r => r.Status == "approved");

        var stats = await approved
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Average = g.Average(r => r.Rating)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var reviews = await approved
            .OrderByDescending(r => r.ModeratedAt ?? r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(PublicReviewLimit)
            .Join(_context.Patients.AsNoTracking(),
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
            .ToListAsync(cancellationToken);

        var count = stats?.Count ?? 0;
        var average = stats == null ? 0 : Math.Round(stats.Average, 1);

        return Ok(new
        {
            average,
            count,
            reviews,
            truncated = count > reviews.Count
        });
    }

    [HttpGet("patient/{patientId:int}")]
    [Authorize]
    public async Task<IActionResult> GetByPatient(int patientId, CancellationToken cancellationToken)
    {
        if (!IsOwnerOrAdmin(patientId)) return Forbid();

        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(PatientReviewCompatibilityLimit + 1)
            .ToListAsync(cancellationToken);

        if (reviews.Count > PatientReviewCompatibilityLimit)
        {
            reviews.RemoveAt(reviews.Count - 1);
            Response.Headers["X-Result-Truncated"] = "true";
        }

        return Ok(reviews);
    }

    [HttpPost("{id:int}/mark-read")]
    [Authorize]
    public async Task<IActionResult> MarkNotificationRead(int id, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews.FindAsync([id], cancellationToken);
        if (review == null) return NotFound();

        if (!IsOwnerOrAdmin(review.PatientId)) return Forbid();

        review.IsNotificationRead = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { review.Id, review.IsNotificationRead });
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req, CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();

        if (req.Rating < 1 || req.Rating > 5)
            return BadRequest(new { message = "❌ Оценка должна быть от 1 до 5" });

        if (string.IsNullOrWhiteSpace(req.Text) || req.Text.Trim().Length < 10)
            return BadRequest(new { message = "❌ Текст отзыва должен содержать не менее 10 символов" });

        var review = new Review
        {
            PatientId = patientId,
            Rating = req.Rating,
            Text = req.Text.Trim(),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

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

    // Server-side moderation pagination. The admin UI uses this endpoint so loading a
    // tab never materializes the complete lifetime review history in EF or the browser.
    [HttpGet("admin/list/{status}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminPage(
        string status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultAdminPageSize,
        CancellationToken cancellationToken = default)
    {
        status = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is not ("pending" or "approved" or "rejected"))
            return BadRequest(new { message = "Недопустимый статус отзывов" });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxAdminPageSize);

        var query = _context.Reviews
            .AsNoTracking()
            .Where(r => r.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_context.Patients.AsNoTracking(),
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
            .ToListAsync(cancellationToken);

        return Ok(new { items, page, pageSize, total, totalPages });
    }

    // Legacy array routes are retained for compatibility, but are now strictly bounded.
    [HttpGet("admin/pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
        => Ok(await AdminCompatibilityQuery("pending", cancellationToken));

    [HttpGet("admin/approved")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetApprovedAdmin(CancellationToken cancellationToken)
        => Ok(await AdminCompatibilityQuery("approved", cancellationToken));

    [HttpGet("admin/rejected")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRejectedAdmin(CancellationToken cancellationToken)
        => Ok(await AdminCompatibilityQuery("rejected", cancellationToken));

    private async Task<object> AdminCompatibilityQuery(string status, CancellationToken cancellationToken)
    {
        var rows = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(AdminReviewCompatibilityLimit + 1)
            .Join(_context.Patients.AsNoTracking(),
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
            .ToListAsync(cancellationToken);

        if (rows.Count > AdminReviewCompatibilityLimit)
        {
            rows.RemoveAt(rows.Count - 1);
            Response.Headers["X-Result-Truncated"] = "true";
        }

        return rows;
    }

    [HttpPut("admin/{id:int}/moderate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Moderate(
        int id,
        [FromBody] ModerateReviewRequest dto,
        CancellationToken cancellationToken)
    {
        // The original replay check was enough for sequential retries, but two
        // instances could still read the same old state before either saved and
        // create duplicate durable notifications. Serialize the full state transition
        // in-process and, on SQL Server, across instances with a transaction-owned
        // application lock scoped to this review id.
        await ProcessModerationGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await BeginModerationTransactionAsync(id, cancellationToken);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (review == null) return NotFound();

            var status = dto.Status?.Trim().ToLowerInvariant();
            if (status != "approved" && status != "rejected")
                return BadRequest(new { message = "❌ Статус должен быть 'approved' или 'rejected'" });

            var rejectionReason = status == "rejected" ? dto.RejectionReason?.Trim() : null;
            if (status == "rejected" && string.IsNullOrWhiteSpace(rejectionReason))
                return BadRequest(new { message = "❌ Укажите причину отклонения отзыва" });

            var currentStatus = review.Status?.Trim().ToLowerInvariant();
            var currentReason = currentStatus == "rejected" ? review.RejectionReason?.Trim() : null;

            // Replaying the same moderation action must not create a second durable
            // notification, move ModeratedAt, or make an already-seen result unread again.
            if (string.Equals(currentStatus, status, StringComparison.Ordinal)
                && string.Equals(currentReason, rejectionReason, StringComparison.Ordinal))
            {
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);

                return Ok(new
                {
                    review.Id,
                    review.Status,
                    review.RejectionReason,
                    idempotent = true
                });
            }

            review.Status = status;
            review.RejectionReason = rejectionReason;
            review.ModeratedAt = DateTime.UtcNow;
            review.IsNotificationRead = false;

            await _context.SaveChangesAsync(cancellationToken);

            var message = status == "approved"
                ? "Ваш отзыв одобрен и опубликован на сайте 🎉"
                : $"Ваш отзыв отклонён. Причина: {review.RejectionReason}";

            // NotificationService uses the same scoped DbContext, so on relational
            // providers both the moderation state and durable patient notification
            // participate in this transaction. Realtime delivery remains best-effort.
            await _notifications.NotifyAsync(
                review.PatientId,
                status == "approved" ? "review_approved" : "review_rejected",
                message,
                review.Id,
                cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                review.Id,
                review.Status,
                review.RejectionReason,
                idempotent = false
            });
        }
        finally
        {
            ProcessModerationGate.Release();
        }
    }

    private async Task<IDbContextTransaction?> BeginModerationTransactionAsync(
        int reviewId,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return null;

        var isolationLevel = _context.Database.IsSqlServer()
            ? IsolationLevel.ReadCommitted
            : IsolationLevel.Serializable;
        var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            if (_context.Database.IsSqlServer())
            {
                var resource = $"dental-review-moderation:{reviewId}";
                await _context.Database.ExecuteSqlInterpolatedAsync($$"""
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource={{resource}},
    @LockMode='Exclusive',
    @LockOwner='Transaction',
    @LockTimeout=5000;
IF @lockResult < 0
    THROW 51000, 'Unable to acquire review moderation lock', 1;
""", cancellationToken);
            }

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsOwnerOrAdmin(int patientId)
    {
        if (User.IsInRole("Admin")) return true;
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) && id == patientId;
    }
}
