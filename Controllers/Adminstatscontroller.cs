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

    public AdminStatsController(ApplicationDbContext db) => _db = db;

    // GET api/adminstats/export/xlsx?from=2026-06-01&to=2026-07-01
    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx([FromQuery] string? from, [FromQuery] string? to)
    {
        var (headers, rows, _) = await BuildAppointmentsTable(from, to);
        var bytes = SimpleXlsxWriter.Write("Заявки", headers, rows);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"zayavki_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
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
        // БАГ (исправлено): admin выбирает даты в своём локальном календаре
        // (как и везде в проекте — см. AppointmentReminderService/DoctorScheduleController),
        // а CreatedAt в БД всегда хранится в UTC. Раньше границы дат сравнивались
        // с CreatedAt напрямую, без конвертации — из-за разницы поясов заявки
        // у самого края диапазона могли попасть не в тот день отчёта (или выпасть
        // из него). Теперь границы явно переводятся из локального времени в UTC
        // перед сравнением с CreatedAt.
        var fromLocal = DateTime.TryParse(from, out var f) ? f.Date : DateTime.Now.AddDays(-30).Date;
        var toLocal = DateTime.TryParse(to, out var t) ? t.Date : DateTime.Now.Date;

        var fromDate = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toDate = DateTime.SpecifyKind(toLocal.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

        var data = await _db.AppointmentRequests
            .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var doctorNames = await _db.Doctors.ToDictionaryAsync(d => d.Id, d => d.FullName);

        var headers = new List<string> { "ID", "Создана", "Имя", "Телефон", "Статус", "Дата приёма", "Врач", "Комментарий" };
        var rows = data.Select(a => (IReadOnlyList<string>)new List<string>
        {
            a.Id.ToString(),
            // CreatedAt хранится в UTC — для отчёта конвертируем в локальное
            // время, иначе админ видит время на несколько часов "не то"
            DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
            a.FirstName ?? "",
            a.Phone ?? "",
            a.Status ?? "",
            a.AppointmentDate?.ToString("dd.MM.yyyy HH:mm") ?? "",
            a.DoctorId.HasValue && doctorNames.TryGetValue(a.DoctorId.Value, out var dn) ? dn : "",
            a.Comment ?? ""
        }).ToList();

        // periodLabel — из fromLocal/toLocal, а не из fromDate/toDate: в заголовке
        // отчёта должны быть даты, которые реально ввёл админ, а не их UTC-эквивалент
        var periodLabel = $"{fromLocal:dd.MM.yyyy} – {toLocal:dd.MM.yyyy}";
        return (headers, rows, periodLabel);
    }
}