using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DoctorScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AppointmentSchedulingService _scheduling;

    public DoctorScheduleController(
        ApplicationDbContext db,
        AppointmentSchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    // GET api/doctorschedule?doctorId=1&from=2026-04-01&to=2026-04-07
    // Legacy event list kept for compatibility with any existing admin integrations.
    [HttpGet]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] int doctorId,
        [FromQuery] string from,
        [FromQuery] string to)
    {
        if (doctorId <= 0) return BadRequest("Нужен doctorId");
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return BadRequest("Нужны параметры from и to (формат YYYY-MM-DD)");

        if (!DateTime.TryParse(from, out var fromDate) ||
            !DateTime.TryParse(to, out var toDate))
        {
            return BadRequest("Неверный формат даты");
        }

        // включительно по to (по локальному времени, без UTC)
        toDate = toDate.Date.AddDays(1).AddTicks(-1);

        var appointments = await _db.AppointmentRequests
            .Where(a => a.DoctorId == doctorId
                        && a.AppointmentDate != null
                        && a.Status == AppointmentStatuses.Confirmed
                        && a.AppointmentDate >= fromDate
                        && a.AppointmentDate <= toDate)
            .ToListAsync();

        // Отдаём строку без Z, чисто локальное ISO
        var result = appointments.Select(a => new
        {
            id = a.Id,
            appointmentDate = a.AppointmentDate.HasValue
                ? a.AppointmentDate.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                : null,
            status = a.Status,
            patientName = a.FirstName
        });

        return Ok(result);
    }

    // GET api/doctorschedule/availability?doctorId=1&from=2026-04-01&to=2026-04-07
    // Источник истины для календаря администратора: шаг слота, длительность,
    // часы работы, lead-time и пересечения совпадают с AppointmentSchedulingService.
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] int doctorId,
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken)
    {
        if (doctorId <= 0) return BadRequest(new { message = "Нужен doctorId" });

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
        {
            return BadRequest(new { message = "Нужны корректные from и to в формате YYYY-MM-DD" });
        }

        if (toDate < fromDate)
            return BadRequest(new { message = "Параметр to не может быть раньше from" });
        if (toDate.DayNumber - fromDate.DayNumber > 31)
            return BadRequest(new { message = "Диапазон календаря не может превышать 32 дня" });

        var doctorExists = await _db.Doctors
            .AnyAsync(d => d.Id == doctorId && d.IsActive, cancellationToken);
        if (!doctorExists)
            return BadRequest(new { message = "Указан несуществующий или неактивный врач" });

        var availability = await _scheduling.GetAvailabilityAsync(
            doctorId,
            fromDate,
            toDate,
            cancellationToken);

        return Ok(availability);
    }
}
