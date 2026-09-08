using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

/// <summary>
/// Аналитика и экспорт для панели администратора.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminStatsController : ControllerBase
{
    private const int MaxExportSpanDays = 366;
    private const int DefaultMaxExportRows = 25_000;
    private const int MinConfiguredExportRows = 100;
    private const int MaxConfiguredExportRows = 100_000;

    private readonly ApplicationDbContext _db;
    private readonly ClinicClock _clock;
    private readonly AdminAnalyticsService _analytics;
    private readonly int _maxExportRows;

    public AdminStatsController(
        ApplicationDbContext db,
        ClinicClock clock,
        AdminAnalyticsService analytics,
        IConfiguration configuration)
    {
        _db = db;
        _clock = clock;
        _analytics = analytics;
        _maxExportRows = Math.Clamp(
            configuration.GetValue<int?>("AdminExports:MaxRows") ?? DefaultMaxExportRows,
            MinConfiguredExportRows,
            MaxConfiguredExportRows);
    }

    // GET api/adminstats/summary
    // Единый серверный источник KPI и данных основных графиков. В отличие от
    // клиентского пересчёта из полного CRM-списка, правила статусов и источников
    // теперь можно менять централизованно и покрывать тестами.
    [HttpGet("summary")]
    public async Task<ActionResult<AdminAnalyticsSummary>> GetSummary(CancellationToken cancellationToken)
        => Ok(await _analytics.GetSummaryAsync(cancellationToken));

    // GET api/adminstats/export/xlsx?from=2026-06-01&to=2026-07-01
    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveExportRange(from, to, out var fromLocal, out var toLocal, out var error))
            return BadRequest(new { message = error });

        var table = await BuildAppointmentsTable(fromLocal, toLocal, cancellationToken);
        if (table.LimitExceeded) return ExportRowLimitExceeded();

        var bytes = SimpleXlsxWriter.Write("Заявки", table.Headers, table.Rows);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"zayavki_{_clock.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // GET api/adminstats/export/report?from=2026-06-01&to=2026-07-01
    // Открывается в новой вкладке, дальше — Ctrl+P → Сохранить как PDF
    [HttpGet("export/report")]
    public async Task<IActionResult> ExportReport(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveExportRange(from, to, out var fromLocal, out var toLocal, out var error))
            return BadRequest(new { message = error });

        var table = await BuildAppointmentsTable(fromLocal, toLocal, cancellationToken);
        if (table.LimitExceeded) return ExportRowLimitExceeded();

        var html = PrintableReportService.BuildReportHtml(
            $"Отчёт по заявкам — {table.PeriodLabel}",
            table.Headers,
            table.Rows,
            _clock.Now);
        return Content(html, "text/html");
    }

    private async Task<AppointmentExportTable> BuildAppointmentsTable(
        DateOnly fromLocal,
        DateOnly toLocal,
        CancellationToken cancellationToken)
    {
        // CreatedAt хранится в UTC, а фильтр отчёта задаётся календарными днями
        // клиники. Границы и отображаемое время всегда считаются через ClinicClock.
        var fromUtc = _clock.ToUtc(fromLocal.ToDateTime(TimeOnly.MinValue));
        var toUtcExclusive = _clock.ToUtc(toLocal.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var headers = new List<string> { "ID", "Создана", "Имя", "Телефон", "Статус", "Дата приёма", "Врач", "Комментарий" };
        var periodLabel = $"{fromLocal:dd.MM.yyyy} – {toLocal:dd.MM.yyyy}";

        // Read one sentinel row beyond the configured ceiling. This proves the
        // export is too large without counting/scanning and materializing the full
        // result set. Oversized exports are rejected rather than silently truncated.
        var data = await _db.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtcExclusive)
            .OrderByDescending(a => a.CreatedAt)
            .Take(_maxExportRows + 1)
            .ToListAsync(cancellationToken);

        if (data.Count > _maxExportRows)
            return new AppointmentExportTable(headers, [], periodLabel, LimitExceeded: true);

        var doctorIds = data
            .Where(a => a.DoctorId.HasValue)
            .Select(a => a.DoctorId!.Value)
            .Distinct()
            .ToList();

        var doctorNames = await _db.Doctors
            .AsNoTracking()
            .Where(d => doctorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.FullName, cancellationToken);

        var rows = data.Select(a => (IReadOnlyList<string>)new List<string>
        {
            a.Id.ToString(CultureInfo.InvariantCulture),
            _clock.FromUtc(a.CreatedAt).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            a.FirstName ?? "",
            a.Phone ?? "",
            a.Status ?? "",
            a.AppointmentDate?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "",
            a.DoctorId.HasValue && doctorNames.TryGetValue(a.DoctorId.Value, out var dn) ? dn : "",
            a.Comment ?? ""
        }).ToList();

        return new AppointmentExportTable(headers, rows, periodLabel, LimitExceeded: false);
    }

    private IActionResult ExportRowLimitExceeded()
        => UnprocessableEntity(new
        {
            message = $"За выбранный период найдено больше {_maxExportRows:N0} записей. Уменьшите период экспорта."
        });

    private bool TryResolveExportRange(
        string? from,
        string? to,
        out DateOnly fromLocal,
        out DateOnly toLocal,
        out string? error)
    {
        var clinicToday = DateOnly.FromDateTime(_clock.Now);
        fromLocal = clinicToday.AddDays(-30);
        toLocal = clinicToday;

        if (!string.IsNullOrWhiteSpace(from)
            && !DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromLocal))
        {
            error = "Параметр from должен быть в формате YYYY-MM-DD";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(to)
            && !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toLocal))
        {
            error = "Параметр to должен быть в формате YYYY-MM-DD";
            return false;
        }

        if (toLocal < fromLocal)
        {
            error = "Параметр to не может быть раньше from";
            return false;
        }

        if (toLocal.DayNumber - fromLocal.DayNumber >= MaxExportSpanDays)
        {
            error = "Период экспорта не может превышать 366 дней";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record AppointmentExportTable(
        List<string> Headers,
        List<IReadOnlyList<string>> Rows,
        string PeriodLabel,
        bool LimitExceeded);
}
