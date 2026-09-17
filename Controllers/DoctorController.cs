using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorController : ControllerBase
{
    private const int PublicDoctorLimit = 200;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];

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
                d.Bio,
                d.RoleTitle,
                d.Education,
                d.Skills,
                d.Philosophy,
                d.Stat2Value,
                d.Stat2Label,
                d.Stat3Value,
                d.Stat3Label,
                d.PhotoUrl))
            .Take(PublicDoctorLimit + 1)
            .ToListAsync(cancellationToken);

        if (doctors.Count > PublicDoctorLimit)
        {
            doctors.RemoveAt(doctors.Count - 1);
            Response.Headers["X-Result-Truncated"] = "true";
        }

        return Ok(doctors);
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAdmin(CancellationToken cancellationToken)
    {
        var doctors = await _db.Doctors
            .AsNoTracking()
            .OrderBy(d => d.FullName)
            .Select(d => new AdminDoctorDto(
                d.Id,
                d.FullName,
                d.FullNameEn,
                d.FullNameFr,
                d.FullNameEl,
                d.FullNameAr,
                d.Specialization,
                d.ExperienceYears,
                d.Bio,
                d.RoleTitle,
                d.Education,
                d.Skills,
                d.Philosophy,
                d.Stat2Value,
                d.Stat2Label,
                d.Stat3Value,
                d.Stat3Label,
                d.PhotoUrl,
                d.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(doctors);
    }

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
            RoleTitle = NormalizeOptional(req.RoleTitle),
            Education = NormalizeMultiline(req.Education),
            Skills = NormalizeMultiline(req.Skills),
            Philosophy = NormalizeOptional(req.Philosophy),
            Stat2Value = NormalizeOptional(req.Stat2Value),
            Stat2Label = NormalizeOptional(req.Stat2Label),
            Stat3Value = NormalizeOptional(req.Stat3Value),
            Stat3Label = NormalizeOptional(req.Stat3Label),
            IsActive = true
        };

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Добавлен новый врач: {FullName} (id={Id})", doctor.FullName, doctor.Id);
        return Ok(ToAdminDto(doctor));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDoctorRequest req,
        CancellationToken cancellationToken)
    {
        var doctor = await _db.Doctors.FindAsync([id], cancellationToken);
        if (doctor == null) return NotFound();

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

        if (!string.IsNullOrWhiteSpace(req.FullName)) doctor.FullName = req.FullName.Trim();
        if (req.FullNameEn != null) doctor.FullNameEn = NormalizeOptional(req.FullNameEn);
        if (req.FullNameFr != null) doctor.FullNameFr = NormalizeOptional(req.FullNameFr);
        if (req.FullNameEl != null) doctor.FullNameEl = NormalizeOptional(req.FullNameEl);
        if (req.FullNameAr != null) doctor.FullNameAr = NormalizeOptional(req.FullNameAr);
        if (req.IsActive.HasValue) doctor.IsActive = req.IsActive.Value;
        if (req.Specialization != null) doctor.Specialization = NormalizeOptional(req.Specialization);

        if (req.ClearExperienceYears) doctor.ExperienceYears = null;
        else if (req.ExperienceYears.HasValue) doctor.ExperienceYears = req.ExperienceYears;

        if (req.Bio != null) doctor.Bio = NormalizeOptional(req.Bio);
        if (req.RoleTitle != null) doctor.RoleTitle = NormalizeOptional(req.RoleTitle);
        if (req.Education != null) doctor.Education = NormalizeMultiline(req.Education);
        if (req.Skills != null) doctor.Skills = NormalizeMultiline(req.Skills);
        if (req.Philosophy != null) doctor.Philosophy = NormalizeOptional(req.Philosophy);
        if (req.Stat2Value != null) doctor.Stat2Value = NormalizeOptional(req.Stat2Value);
        if (req.Stat2Label != null) doctor.Stat2Label = NormalizeOptional(req.Stat2Label);
        if (req.Stat3Value != null) doctor.Stat3Value = NormalizeOptional(req.Stat3Value);
        if (req.Stat3Label != null) doctor.Stat3Label = NormalizeOptional(req.Stat3Label);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Обновлён врач id={Id}: {FullName}, активен={IsActive}", doctor.Id, doctor.FullName, doctor.IsActive);
        return Ok(ToAdminDto(doctor));
    }

    [HttpPost("{id:int}/photo")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxPhotoBytes + 2048)]
    public async Task<IActionResult> UploadPhoto(
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Выберите фотографию врача" });
        if (file.Length > MaxPhotoBytes)
            return BadRequest(new { message = "Фотография слишком большая. Максимум 5 МБ." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(extension))
            return BadRequest(new { message = "Разрешены JPG, PNG и WEBP" });

        await using (var validation = file.OpenReadStream())
        {
            if (!await ImageUploadValidator.MatchesExtensionAsync(validation, extension, cancellationToken))
                return BadRequest(new { message = "Файл не является корректным изображением" });
        }

        var doctor = await _db.Doctors.FindAsync([id], cancellationToken);
        if (doctor == null) return NotFound();

        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);

        doctor.PhotoData = memory.ToArray();
        doctor.PhotoContentType = ContentTypeForExtension(extension);
        doctor.PhotoUrl = $"/api/doctor/{doctor.Id}/photo?v={Guid.NewGuid():N}";
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { doctor.PhotoUrl });
    }

    [HttpDelete("{id:int}/photo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken cancellationToken)
    {
        var doctor = await _db.Doctors.FindAsync([id], cancellationToken);
        if (doctor == null) return NotFound();

        doctor.PhotoData = null;
        doctor.PhotoContentType = null;
        doctor.PhotoUrl = null;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Фотография врача удалена" });
    }

    [HttpGet("{id:int}/photo")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var photo = await _db.Doctors
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.PhotoData, d.PhotoContentType })
            .SingleOrDefaultAsync(cancellationToken);

        if (photo?.PhotoData == null || photo.PhotoData.Length == 0 || string.IsNullOrWhiteSpace(photo.PhotoContentType))
            return NotFound();

        return File(photo.PhotoData, photo.PhotoContentType);
    }

    private static AdminDoctorDto ToAdminDto(Doctor d) => new(
        d.Id, d.FullName, d.FullNameEn, d.FullNameFr, d.FullNameEl, d.FullNameAr,
        d.Specialization, d.ExperienceYears, d.Bio, d.RoleTitle, d.Education, d.Skills,
        d.Philosophy, d.Stat2Value, d.Stat2Label, d.Stat3Value, d.Stat3Label, d.PhotoUrl, d.IsActive);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var lines = value.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? null : string.Join('\n', lines);
    }

    private static string ContentTypeForExtension(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
