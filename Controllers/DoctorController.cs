using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ClinicClock _clock;
    private readonly ILogger<DoctorController> _logger;

    public DoctorController(
        ApplicationDbContext db,
        ClinicClock clock,
        ILogger<DoctorController> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    // Публично: только активные врачи и только поля, необходимые публичному сайту.
    // Явная проекция защищает API от случайной публикации будущих внутренних полей Doctor.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var doctors = await _db.Doctors
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.FullName)
            .Select(d => new PublicDoctorDto(
                d.Id,
                d.FullName,
                d.FullNameEn,
                d.FullNameFr,
                d.FullNameEl,
                d.FullNameAr,
                d.Specialization,
                d.ExperienceYears,
                d.Bio))
            .ToListAsync(cancellationToken);

        return Ok(doctors);
    }

    // Админ: все врачи, включая деактивированных — для управления списком
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAdmin(CancellationToken cancellationToken)
    {
        var doctors = await _db.Doctors
            .AsNoTracking()
            .OrderBy(d => d.FullName)
            .ToListAsync(cancellationToken);

        return Ok(doctors);
    }

    // Админ: добавить нового врача
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDoctorRequest req,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Укажите имя врача" });

        var doctor = new Doctor
        {
            FullName = req.FullName.Trim(),
            FullNameEn = NormalizeOptional(req.FullNameEn),
            FullNameFr = NormalizeOptional(req.FullNameFr),
            FullNameEl = NormalizeOptional(req.FullNameEl),
            FullNameAr = NormalizeOptional(req.FullNameAr),
            Specialization = NormalizeOptional(req.Specialization),
            ExperienceYears = req.ExperienceYears,
            Bio = NormalizeOptional(req.Bio),
            IsActive = true
        };

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Добавлен новый врач: {FullName} (id={Id})", doctor.FullName, doctor.Id);

        return Ok(doctor);
    }

    // Админ: изменить профиль врача и/или активность (деактивировать вместо удаления,
    // чтобы не потерять историю приёмов, где он указан как DoctorId).
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDoctorRequest req,
        CancellationToken cancellationToken)
    {
        var doctor = await _db.Doctors.FindAsync([id], cancellationToken);
        if (doctor == null) return NotFound();

        // Деактивация скрывает врача с публичного сайта и запрещает новые записи.
        // Если у него уже есть будущие pending/confirmed записи, сначала их нужно
        // перенести/отменить — иначе CRM сохранит подтверждённые приёмы у врача,
        // которого календарь и публичный каталог больше не считают доступным.
        if (doctor.IsActive && req.IsActive == false)
        {
            var clinicNow = _clock.Now;
            var futureAppointments = await _db.AppointmentRequests
                .AsNoTracking()
                .CountAsync(a =>
                    a.DoctorId == doctor.Id
                    && a.AppointmentDate.HasValue
                    && a.AppointmentDate.Value >= clinicNow
                    && (a.Status == AppointmentStatuses.Pending
                        || a.Status == AppointmentStatuses.Confirmed),
                    cancellationToken);

            if (futureAppointments > 0)
            {
                return Conflict(new
                {
                    message = "Нельзя деактивировать врача, пока у него есть будущие ожидающие или подтверждённые записи. Сначала перенесите или отмените их.",
                    futureAppointments
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(req.FullName))
            doctor.FullName = req.FullName.Trim();

        if (req.FullNameEn != null)
            doctor.FullNameEn = NormalizeOptional(req.FullNameEn);
        if (req.FullNameFr != null)
            doctor.FullNameFr = NormalizeOptional(req.FullNameFr);
        if (req.FullNameEl != null)
            doctor.FullNameEl = NormalizeOptional(req.FullNameEl);
        if (req.FullNameAr != null)
            doctor.FullNameAr = NormalizeOptional(req.FullNameAr);

        if (req.IsActive.HasValue)
            doctor.IsActive = req.IsActive.Value;

        if (req.Specialization != null)
            doctor.Specialization = NormalizeOptional(req.Specialization);

        if (req.ClearExperienceYears)
            doctor.ExperienceYears = null;
        else if (req.ExperienceYears.HasValue)
            doctor.ExperienceYears = req.ExperienceYears;

        if (req.Bio != null)
            doctor.Bio = NormalizeOptional(req.Bio);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Обновлён врач id={Id}: {FullName}, активен={IsActive}", doctor.Id, doctor.FullName, doctor.IsActive);

        return Ok(doctor);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }
}
