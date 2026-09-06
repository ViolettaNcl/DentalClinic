using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;
using DentalClinic.Data;
using DentalClinic.Services;
using System.Security.Claims;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentRequestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppointmentRequestController> _logger;
    private readonly NotificationService _notifications;
    private readonly AppointmentSchedulingService _scheduling;

    public AppointmentRequestController(
        ApplicationDbContext context,
        ILogger<AppointmentRequestController> logger,
        NotificationService notifications,
        AppointmentSchedulingService scheduling)
    {
        _context = context;
        _logger = logger;
        _notifications = notifications;
        _scheduling = scheduling;
    }

    // Заявки конкретного пациента — только сам пациент (по токену) или админ.
    // Legacy UI still consumes an array, so keep active work plus a bounded slice of
    // history instead of materializing the patient's entire lifetime history.
    [HttpGet("patient/{patientId:int}")]
    [Authorize]
    public async Task<IActionResult> GetPatient(int patientId, CancellationToken cancellationToken)
    {
        if (!IsOwnerOrAdmin(patientId)) return Forbid();

        var lang = Request.Headers["Accept-Language"].ToString().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lang))
            lang = "ru";

        var patientRequests = _context.AppointmentRequests
            .AsNoTracking()
            .Where(r => r.PatientId == patientId);

        var active = await AppointmentReadPolicy.ReadAsync(
            patientRequests
                .Where(r => r.Status == AppointmentStatuses.Pending || r.Status == AppointmentStatuses.Confirmed)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new
                {
                    r.Id,
                    r.PatientId,
                    r.FirstName,
                    r.Phone,
                    r.AppointmentDate,
                    r.Comment,
                    r.Status,
                    r.CreatedAt,
                    r.DoctorId,
                    DoctorName = r.DoctorId != null
                        ? _context.Doctors
                            .Where(d => d.Id == r.DoctorId)
                            .Select(d =>
                                lang.StartsWith("en") ? (d.FullNameEn ?? d.FullName) :
                                lang.StartsWith("fr") ? (d.FullNameFr ?? d.FullName) :
                                lang.StartsWith("el") ? (d.FullNameEl ?? d.FullName) :
                                lang.StartsWith("ar") ? (d.FullNameAr ?? d.FullName) :
                                d.FullName)
                            .FirstOrDefault()
                        : null
                }),
            AppointmentReadPolicy.PatientActiveLimit,
            cancellationToken);

        var history = await AppointmentReadPolicy.ReadAsync(
            patientRequests
                .Where(r => r.Status == AppointmentStatuses.Completed || r.Status == AppointmentStatuses.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new
                {
                    r.Id,
                    r.PatientId,
                    r.FirstName,
                    r.Phone,
                    r.AppointmentDate,
                    r.Comment,
                    r.Status,
                    r.CreatedAt,
                    r.DoctorId,
                    DoctorName = r.DoctorId != null
                        ? _context.Doctors
                            .Where(d => d.Id == r.DoctorId)
                            .Select(d =>
                                lang.StartsWith("en") ? (d.FullNameEn ?? d.FullName) :
                                lang.StartsWith("fr") ? (d.FullNameFr ?? d.FullName) :
                                lang.StartsWith("el") ? (d.FullNameEl ?? d.FullName) :
                                lang.StartsWith("ar") ? (d.FullNameAr ?? d.FullName) :
                                d.FullName)
                            .FirstOrDefault()
                        : null
                }),
            AppointmentReadPolicy.PatientHistoryLimit,
            cancellationToken);

        MarkReadTruncation(active.Truncated, history.Truncated);

        var data = active.Items
            .Concat(history.Items)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToList();

        return Ok(data);
    }

    // Compatibility feed for the admin dashboard. It deliberately keeps all recent
    // live work before bounded history; the dedicated paged history path can retrieve
    // older rows without ever loading the complete table into one HTTP response.
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var requests = _context.AppointmentRequests.AsNoTracking();

        var active = await AppointmentReadPolicy.ReadAsync(
            requests
                .Where(r => r.Status == AppointmentStatuses.Pending || r.Status == AppointmentStatuses.Confirmed)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id),
            AppointmentReadPolicy.AdminActiveLimit,
            cancellationToken);

        var history = await AppointmentReadPolicy.ReadAsync(
            requests
                .Where(r => r.Status == AppointmentStatuses.Completed || r.Status == AppointmentStatuses.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id),
            AppointmentReadPolicy.AdminHistoryLimit,
            cancellationToken);

        MarkReadTruncation(active.Truncated, history.Truncated);

        var data = active.Items
            .Concat(history.Items)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToList();

        return Ok(data);
    }

    // Создать заявку (гость или зарегистрированный).
    // Если запрос пришёл с валидным токеном пациента — PatientId берём из токена,
    // а не из тела запроса (иначе можно было бы записаться "от лица" другого пациента).
    [HttpPost]
    [EnableRateLimiting("AppointmentCreate")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest dto,
        CancellationToken cancellationToken)
    {
        var appointmentDate = dto.AppointmentDate.HasValue
            ? _scheduling.Normalize(dto.AppointmentDate.Value)
            : (DateTime?)null;

        if (dto.DoctorId.HasValue && !appointmentDate.HasValue)
            return BadRequest(new { message = "Для выбора врача укажите время приёма" });

        await using var transaction = await BeginSchedulingTransactionAsync(cancellationToken);

        if (appointmentDate.HasValue)
        {
            var validation = await _scheduling.ValidateAsync(
                appointmentDate.Value,
                dto.DoctorId,
                allowDateOnly: !dto.DoctorId.HasValue,
                cancellationToken: cancellationToken);
            if (!validation.IsValid) return SchedulingError(validation);
        }

        var request = new AppointmentRequest
        {
            FirstName = dto.FirstName?.Trim(),
            Phone = dto.Phone.Trim(),
            AppointmentDate = appointmentDate,
            Comment = dto.Comment?.Trim(),
            DoctorId = dto.DoctorId,
            CreatedAt = DateTime.UtcNow,
            Status = AppointmentStatuses.Pending,
            ReminderSent = false
        };

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Patient"))
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            request.PatientId = idClaim != null ? int.Parse(idClaim) : null;
        }
        else
        {
            request.PatientId = null;
        }

        try
        {
            _context.AppointmentRequests.Add(request);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании заявки на приём");
            return StatusCode(500, new { message = "Не удалось создать заявку, попробуйте позже" });
        }

        _logger.LogInformation("Создана заявка на приём #{Id}", request.Id);

        return Ok(new { id = request.Id, message = "Заявка создана" });
    }

    // Редактировать заявку (админ)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAppointmentRequest dto,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSchedulingTransactionAsync(cancellationToken);

        var request = await _context.AppointmentRequests.FindAsync([id], cancellationToken);
        if (request == null) return NotFound();

        if (!AppointmentStatuses.TryNormalize(request.Status, out var previousStatus))
            return Conflict(new { message = "У заявки сохранён неизвестный статус. Исправьте данные вручную" });

        var nextStatus = previousStatus;
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            if (!AppointmentStatuses.TryNormalize(dto.Status, out nextStatus))
                return BadRequest(new { message = "Недопустимый статус заявки" });

            if (!AppointmentStatuses.CanTransition(previousStatus, nextStatus))
                return BadRequest(new { message = $"Переход статуса {previousStatus} → {nextStatus} запрещён" });
        }

        // Explicit JSON null now means "clear this field", while an omitted property
        // means "leave it unchanged". UpdateAppointmentRequest tracks property presence
        // in its setters, which preserves partial-update semantics for nullable values.
        var nextDate = dto.AppointmentDateSpecified
            ? dto.AppointmentDate.HasValue
                ? _scheduling.Normalize(dto.AppointmentDate.Value)
                : null
            : request.AppointmentDate;
        var nextDoctorId = dto.DoctorIdSpecified ? dto.DoctorId : request.DoctorId;
        var scheduleChanged = dto.AppointmentDateSpecified || dto.DoctorIdSpecified;
        var becomingConfirmed = previousStatus != AppointmentStatuses.Confirmed
            && nextStatus == AppointmentStatuses.Confirmed;
        var reactivating = previousStatus != AppointmentStatuses.Pending
            && nextStatus == AppointmentStatuses.Pending;

        // DoctorId is a scheduled resource assignment, not just CRM metadata.
        // Never persist a doctor without an actual appointment date/time: that state
        // cannot be represented correctly in the availability calendar and later
        // confirmation would inherit an incomplete schedule.
        if (nextDoctorId.HasValue && !nextDate.HasValue)
            return BadRequest(new { message = "Для выбора врача укажите время приёма" });

        if (nextStatus == AppointmentStatuses.Confirmed && (!nextDate.HasValue || !nextDoctorId.HasValue))
            return BadRequest(new { message = "Для подтверждения укажите врача и время приёма" });

        if (nextDate.HasValue && (scheduleChanged || becomingConfirmed || reactivating))
        {
            var validation = await _scheduling.ValidateAsync(
                nextDate.Value,
                nextDoctorId,
                request.Id,
                allowDateOnly: !nextDoctorId.HasValue,
                cancellationToken: cancellationToken);
            if (!validation.IsValid) return SchedulingError(validation);
        }

        if (dto.AppointmentDateSpecified)
        {
            request.AppointmentDate = nextDate;
            request.ReminderSent = false;
        }

        if (dto.CommentSpecified)
            request.Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();

        request.Status = nextStatus;

        if (dto.DoctorIdSpecified)
        {
            request.DoctorId = dto.DoctorId;
            request.ReminderSent = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        // Уведомляем пациента о смене статуса (только если это зарегистрированный
        // пациент, а не гостевая запись, и статус реально изменился)
        if (request.PatientId.HasValue && request.Status != previousStatus)
        {
            var dateText = request.AppointmentDate.HasValue
                ? request.AppointmentDate.Value.ToString("dd.MM.yyyy HH:mm")
                : "уточняется";

            var (type, message) = request.Status switch
            {
                AppointmentStatuses.Confirmed => ("appointment_confirmed", $"Ваша запись на {dateText} подтверждена ✅"),
                AppointmentStatuses.Cancelled => ("appointment_cancelled", $"Ваша запись на {dateText} отклонена администратором"),
                AppointmentStatuses.Completed => ("appointment_completed", $"Приём {dateText} отмечен как завершённый. Будем рады видеть вас снова!"),
                _ => (null, null)
            };

            if (type != null)
                await _notifications.NotifyAsync(request.PatientId.Value, type, message!, request.Id);
        }

        return Ok(new { request.Id, request.Status, request.AppointmentDate, request.Comment, request.DoctorId });
    }

    // Создать запись по телефону (админ)
    [HttpPost("admin/phone")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFromPhone(
        [FromBody] AdminPhoneAppointmentDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest(new { message = "Имя и телефон обязательны" });

        if (!dto.AppointmentDate.HasValue || !dto.DoctorId.HasValue)
            return BadRequest(new { message = "Для подтверждённой записи укажите врача и время приёма" });

        var appointmentDate = _scheduling.Normalize(dto.AppointmentDate.Value);
        await using var transaction = await BeginSchedulingTransactionAsync(cancellationToken);

        var validation = await _scheduling.ValidateAsync(
            appointmentDate,
            dto.DoctorId,
            cancellationToken: cancellationToken);
        if (!validation.IsValid) return SchedulingError(validation);

        var request = new AppointmentRequest
        {
            FirstName = dto.FirstName.Trim(),
            Phone = dto.Phone.Trim(),
            Comment = dto.Comment?.Trim(),
            AppointmentDate = appointmentDate,
            DoctorId = dto.DoctorId,
            Status = AppointmentStatuses.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        _context.AppointmentRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return Ok(new { id = request.Id, message = "Телефонная запись создана" });
    }

    // Пациент: отменить свою запись (можно отменить только ожидающую или подтверждённую)
    [HttpPut("{id:int}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOwn(int id, CancellationToken cancellationToken)
    {
        var request = await _context.AppointmentRequests.FindAsync([id], cancellationToken);
        if (request == null) return NotFound();

        if (!IsOwnerOfAppointment(request)) return Forbid();

        var status = request.Status?.ToLowerInvariant();
        if (status == AppointmentStatuses.Confirmed)
            return BadRequest(new { message = "Подтверждённую запись нельзя изменить самостоятельно — пожалуйста, позвоните администратору клиники: +7 (499) 999-99-99" });
        if (status != AppointmentStatuses.Pending)
            return BadRequest(new { message = "Эту запись уже нельзя отменить" });

        request.Status = AppointmentStatuses.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Пациент отменил свою запись #{Id}", request.Id);

        return Ok(new { request.Id, request.Status });
    }

    // Пациент: перенести свою запись на другое время.
    // Статус возвращается в "pending" — новое время требует повторного
    // подтверждения администратором.
    [HttpPut("{id:int}/reschedule")]
    [Authorize]
    public async Task<IActionResult> RescheduleOwn(
        int id,
        [FromBody] PatientRescheduleRequest dto,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSchedulingTransactionAsync(cancellationToken);

        var request = await _context.AppointmentRequests.FindAsync([id], cancellationToken);
        if (request == null) return NotFound();

        if (!IsOwnerOfAppointment(request)) return Forbid();

        var status = request.Status?.ToLowerInvariant();
        if (status == AppointmentStatuses.Confirmed)
            return BadRequest(new { message = "Подтверждённую запись нельзя перенести самостоятельно — пожалуйста, позвоните администратору клиники: +7 (499) 999-99-99" });
        if (status != AppointmentStatuses.Pending)
            return BadRequest(new { message = "Эту запись уже нельзя перенести" });

        var appointmentDate = _scheduling.Normalize(dto.AppointmentDate);
        var validation = await _scheduling.ValidateAsync(
            appointmentDate,
            request.DoctorId,
            request.Id,
            allowDateOnly: !request.DoctorId.HasValue,
            cancellationToken: cancellationToken);
        if (!validation.IsValid) return SchedulingError(validation);

        request.AppointmentDate = appointmentDate;
        request.Status = AppointmentStatuses.Pending;
        request.ReminderSent = false;
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Пациент перенёс свою запись #{Id} на {Date}", request.Id, dto.AppointmentDate);

        return Ok(new { request.Id, request.Status, request.AppointmentDate });
    }

    private bool IsOwnerOrAdmin(int patientId)
    {
        if (User.IsInRole("Admin")) return true;
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return idClaim != null && int.Parse(idClaim) == patientId;
    }

    // Гостевые заявки (PatientId == null) не принадлежат ни одному пациенту —
    // ими может управлять только администратор.
    private bool IsOwnerOfAppointment(AppointmentRequest request)
    {
        if (User.IsInRole("Admin")) return true;
        return request.PatientId.HasValue && IsOwnerOrAdmin(request.PatientId.Value);
    }

    private void MarkReadTruncation(bool activeTruncated, bool historyTruncated)
    {
        if (!activeTruncated && !historyTruncated) return;

        Response.Headers["X-Result-Truncated"] = "true";
        Response.Headers["X-Active-Truncated"] = activeTruncated ? "true" : "false";
        Response.Headers["X-History-Truncated"] = historyTruncated ? "true" : "false";
    }

    private IActionResult SchedulingError(SchedulingValidationResult validation) =>
        StatusCode(validation.StatusCode, new { message = validation.Message });

    private async Task<IDbContextTransaction?> BeginSchedulingTransactionAsync(CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational()) return null;
        return await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }
}
