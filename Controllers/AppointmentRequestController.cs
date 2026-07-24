using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;
using DentalClinic.Data;
using DentalClinic.Services;
using System.Security.Claims;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentRequestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppointmentRequestController> _logger;
    private readonly NotificationService _notifications;

    public AppointmentRequestController(
        ApplicationDbContext context,
        ILogger<AppointmentRequestController> logger,
        NotificationService notifications)
    {
        _context = context;
        _logger = logger;
        _notifications = notifications;
    }

    // Заявки конкретного пациента — только сам пациент (по токену) или админ
    [HttpGet("patient/{patientId:int}")]
    [Authorize]
    public async Task<IActionResult> GetPatient(int patientId)
    {
        if (!IsOwnerOrAdmin(patientId)) return Forbid();

        var lang = Request.Headers["Accept-Language"].ToString().ToLower();

        if (string.IsNullOrWhiteSpace(lang))
            lang = "ru";

        var data = await _context.AppointmentRequests
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
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
            })
            .ToListAsync();

        return Ok(data);
    }

    // Все заявки (админ)
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
        => Ok(await _context.AppointmentRequests
               .OrderByDescending(r => r.CreatedAt)
               .ToListAsync());

    // Создать заявку (гость или зарегистрированный).
    // Если запрос пришёл с валидным токеном пациента — PatientId берём из токена,
    // а не из тела запроса (иначе можно было бы записаться "от лица" другого пациента).
    [HttpPost]
    [EnableRateLimiting("AppointmentCreate")]
    public async Task<IActionResult> Create([FromBody] AppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Телефон обязателен" });

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Patient"))
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            request.PatientId = idClaim != null ? int.Parse(idClaim) : null;
        }
        else
        {
            request.PatientId = null;
        }

        request.CreatedAt = DateTime.UtcNow;
        request.Status ??= "pending";

        try
        {
            _context.AppointmentRequests.Add(request);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании заявки на приём (телефон: {Phone})", request.Phone);
            return StatusCode(500, new { message = "Не удалось создать заявку, попробуйте позже" });
        }

        _logger.LogInformation("Создана заявка на приём #{Id} (телефон: {Phone})", request.Id, request.Phone);

        return Ok(new { id = request.Id, message = "Заявка создана" });
    }

    // Редактировать заявку (админ)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentRequest dto)
    {
        var request = await _context.AppointmentRequests.FindAsync(id);
        if (request == null) return NotFound();

        var previousStatus = request.Status;

        if (dto.AppointmentDate.HasValue) request.AppointmentDate = dto.AppointmentDate;
        if (!string.IsNullOrWhiteSpace(dto.Comment)) request.Comment = dto.Comment;
        if (!string.IsNullOrWhiteSpace(dto.Status)) request.Status = dto.Status;

        if (dto.DoctorId.HasValue)
        {
            if (!await _context.Doctors.AnyAsync(d => d.Id == dto.DoctorId.Value && d.IsActive))
                return BadRequest(new { message = "Указан несуществующий или неактивный врач" });

            request.DoctorId = dto.DoctorId;
        }

        await _context.SaveChangesAsync();

        // Уведомляем пациента о смене статуса (только если это зарегистрированный
        // пациент, а не гостевая запись, и статус реально изменился)
        if (request.PatientId.HasValue && request.Status != previousStatus)
        {
            var dateText = request.AppointmentDate.HasValue
                ? request.AppointmentDate.Value.ToString("dd.MM.yyyy HH:mm")
                : "уточняется";

            var (type, message) = request.Status?.ToLower() switch
            {
                "confirmed" => ("appointment_confirmed", $"Ваша запись на {dateText} подтверждена ✅"),
                "cancelled" => ("appointment_cancelled", $"Ваша запись на {dateText} отклонена администратором"),
                "completed" => ("appointment_completed", $"Приём {dateText} отмечен как завершённый. Будем рады видеть вас снова!"),
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
    public async Task<IActionResult> CreateFromPhone([FromBody] AdminPhoneAppointmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest(new { message = "Имя и телефон обязательны" });

        if (dto.DoctorId.HasValue &&
            !await _context.Doctors.AnyAsync(d => d.Id == dto.DoctorId.Value && d.IsActive))
            return BadRequest(new { message = "Указан несуществующий или неактивный врач" });

        var request = new AppointmentRequest
        {
            FirstName = dto.FirstName,
            Phone = dto.Phone,
            Comment = dto.Comment,
            AppointmentDate = dto.AppointmentDate,
            DoctorId = dto.DoctorId,
            Status = "confirmed",
            CreatedAt = DateTime.UtcNow
        };

        _context.AppointmentRequests.Add(request);
        await _context.SaveChangesAsync();

        return Ok(new { id = request.Id, message = "Телефонная запись создана" });
    }

    // Пациент: отменить свою запись (можно отменить только ожидающую или подтверждённую)
    [HttpPut("{id:int}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOwn(int id)
    {
        var request = await _context.AppointmentRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (!IsOwnerOfAppointment(request)) return Forbid();

        var status = request.Status?.ToLower();
        if (status == "confirmed")
            return BadRequest(new { message = "Подтверждённую запись нельзя изменить самостоятельно — пожалуйста, позвоните администратору клиники: +7 (499) 999-99-99" });
        if (status != "pending")
            return BadRequest(new { message = "Эту запись уже нельзя отменить" });

        request.Status = "cancelled";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Пациент отменил свою запись #{Id}", request.Id);

        return Ok(new { request.Id, request.Status });
    }

    // Пациент: перенести свою запись на другое время.
    // Статус возвращается в "pending" — новое время требует повторного
    // подтверждения администратором.
    [HttpPut("{id:int}/reschedule")]
    [Authorize]
    public async Task<IActionResult> RescheduleOwn(int id, [FromBody] PatientRescheduleRequest dto)
    {
        var request = await _context.AppointmentRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (!IsOwnerOfAppointment(request)) return Forbid();

        var status = request.Status?.ToLower();
        if (status == "confirmed")
            return BadRequest(new { message = "Подтверждённую запись нельзя перенести самостоятельно — пожалуйста, позвоните администратору клиники: +7 (499) 999-99-99" });
        if (status != "pending")
            return BadRequest(new { message = "Эту запись уже нельзя перенести" });

        request.AppointmentDate = dto.AppointmentDate;
        request.Status = "pending";
        await _context.SaveChangesAsync();

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
}