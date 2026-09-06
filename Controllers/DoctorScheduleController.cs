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
    private const int MaxCalendarSpanDays = 31;

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
        [FromQuery] string to,
        CancellationToken cancellationToken)
    {
        if (doctorId <= 0)
            return BadRequest(new { message = "Нужен doctorId" });

        if (!TryParseCalendarRange(from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(new { message = error });

        var doctorExists = await _db.Doctors
            .AsNoTracking()
            .AnyAsync(d => d.Id == doctorId && d.IsActive, cancellationToken);
        if (!doctorExists)
            return BadRequest(new { message = "Указан несуществующий или неактивный врач" });

        // AppointmentDate хранится как локальное время без смещения. Используем
        // полуоткрытый диапазон [from, day-after-to), чтобы не зависеть от точности
        // DateTime и не строить конец дня через AddTicks(-1).
        var fromDateTime = fromDate.ToDateTime(TimeOnly.MinValue);
        var toExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var appointments = await _db.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                        && a.AppointmentDate != null
                        && a.Status == AppointmentStatuses.Confirmed
                        && a.AppointmentDate >= fromDateTime
                        && a.AppointmentDate < toExclusive)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync(cancellationToken);

        // Отдаём строку без Z, чисто локальное ISO
        var result = appointments.Select(a => new
        {
            id = a.Id,
            appointmentDate = a.AppointmentDate.HasValue
                ? a.AppointmentDate.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
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
        if (doctorId <= 0)
            return BadRequest(new { message = "Нужен doctorId" });

        if (!TryParseCalendarRange(from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(new { message = error });

        var doctorExists = await _db.Doctors
            .AsNoTracking()
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

    private static bool TryParseCalendarRange(
        string? from,
        string? to,
        out DateOnly fromDate,
        out DateOnly toDate,
        out string? error)
    {
        var fromIsValid = DateOnly.TryParseExact(
            from,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fromDate);
        var toIsValid = DateOnly.TryParseExact(
            to,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out toDate);

        if (!fromIsValid || !toIsValid)
        {
            error = "Нужны корректные from и to в формате YYYY-MM-DD";
            return false;
        }

        if (toDate < fromDate)
        {
            error = "Параметр to не может быть раньше from";
            return false;
        }

        if (toDate.DayNumber - fromDate.DayNumber > MaxCalendarSpanDays)
        {
            error = "Диапазон календаря не может превышать 32 дня";
            return false;
        }

        error = null;
        return true;
    }
}
