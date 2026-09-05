using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

/// <summary>
/// Экспорт данных для вкладки "Аналитика" в админке (сами графики уже считаются
/// на фронте в AnalyticsManager из /api/appointmentrequest/admin/all — это отдельно
/// не трогаем, тут только выгрузка в файл).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminStatsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ClinicClock _clock;

    public AdminStatsController(ApplicationDbContext db, ClinicClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // GET api/adminstats/export/xlsx?from=2026-06-01&to=2026-07-01
    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx([FromQuery] string? from, [FromQuery] string? to)
    {
        var (headers, rows, _) = await BuildAppointmentsTable(from, to);
        var bytes = SimpleXlsxWriter.Write("Заявки", headers, rows);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"zayavki_{_clock.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // GET api/adminstats/export/report?from=2026-06-01&to=2026-07-01
    // Открывается в новой вкладке, дальше — Ctrl+P → Сохранить как PDF
    [HttpGet("export/report")]
    public async Task<IActionResult> ExportReport([FromQuery] string? from, [FromQuery] string? to)
    {
        var (headers, rows, periodLabel) = await BuildAppointmentsTable(from, to);
        var html = PrintableReportService.BuildReportHtml($"Отчёт по заявкам — {periodLabel}", headers, rows);
        return Content(html, "text/html");
    }

    private async Task<(List<string> headers, List<IReadOnlyList<string>> rows, string periodLabel)> BuildAppointmentsTable(string? from, string? to)
    {
        // CreatedAt хранится в UTC, а фильтр отчёта задаётся календарными днями
        // клиники. На Vercel локальный часовой пояс контейнера может быть UTC,
        // поэтому DateTime.ToLocalTime() здесь использовать нельзя: границы и
        // отображаемое время должны всегда считаться через ClinicClock.
        var clinicToday = DateOnly.FromDateTime(_clock.Now);
        var fromLocal = ParseDate(from, clinicToday.AddDays(-30));
        var toLocal = ParseDate(to, clinicToday);

        var fromUtc = _clock.ToUtc(fromLocal.ToDateTime(TimeOnly.MinValue));
        var toUtcExclusive = _clock.ToUtc(toLocal.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var data = await _db.AppointmentRequests
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtcExclusive)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var doctorNames = await _db.Doctors.ToDictionaryAsync(d => d.Id, d => d.FullName);

        var headers = new List<string> { "ID", "Создана", "Имя", "Телефон", "Статус", "Дата приёма", "Врач", "Комментарий" };
        var rows = data.Select(a => (IReadOnlyList<string>)new List<string>
        {
            a.Id.ToString(),
            _clock.FromUtc(a.CreatedAt).ToString("dd.MM.yyyy HH:mm"),
            a.FirstName ?? "",
            a.Phone ?? "",
            a.Status ?? "",
            a.AppointmentDate?.ToString("dd.MM.yyyy HH:mm") ?? "",
            a.DoctorId.HasValue && doctorNames.TryGetValue(a.DoctorId.Value, out var dn) ? dn : "",
            a.Comment ?? ""
        }).ToList();

        var periodLabel = $"{fromLocal:dd.MM.yyyy} – {toLocal:dd.MM.yyyy}";
        return (headers, rows, periodLabel);
    }

    private static DateOnly ParseDate(string? value, DateOnly fallback) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : fallback;
}
