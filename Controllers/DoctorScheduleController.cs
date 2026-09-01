using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DoctorScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DoctorScheduleController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET api/doctorschedule?doctorId=1&from=2026-04-01&to=2026-04-07
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
}
