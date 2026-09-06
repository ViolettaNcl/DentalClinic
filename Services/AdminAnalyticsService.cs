using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Server-side source of truth for the appointment KPI cards and charts shown in
/// the admin analytics dashboard. Keeping the aggregation here prevents the UI
/// from re-implementing status/source rules differently in every chart.
/// </summary>
public sealed class AdminAnalyticsService
{
    private const string DentaMarker = "[Заявка через чат]";

    private readonly ApplicationDbContext _db;
    private readonly ClinicClock _clock;

    public AdminAnalyticsService(ApplicationDbContext db, ClinicClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AdminAnalyticsSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var clinicNow = _clock.Now;

        // Aggregate the lifetime cards in SQL instead of materializing every
        // appointment request into the web process. This keeps dashboard memory
        // bounded as CRM history grows while preserving the legacy normalization
        // rules for status values with casing/whitespace drift.
        var statusGroups = await _db.AppointmentRequests
            .AsNoTracking()
            .GroupBy(a => a.Status == null ? string.Empty : a.Status.Trim().ToLower())
            .Select(group => new StatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var total = statusGroups.Sum(group => group.Count);
        var pending = CountStatus(statusGroups, AppointmentStatuses.Pending);
        var confirmed = CountStatus(statusGroups, AppointmentStatuses.Confirmed);
        var completed = CountStatus(statusGroups, AppointmentStatuses.Completed);
        var cancelled = CountStatus(statusGroups, AppointmentStatuses.Cancelled);
        var unknown = total - pending - confirmed - completed - cancelled;
        var confirmedLike = confirmed + completed;
        var confirmedOrCompletedRate = total == 0
            ? 0
            : Math.Round(confirmedLike * 100d / total, 1, MidpointRounding.AwayFromZero);

        // Sources are deliberately mutually exclusive. Denta wins even when a
        // signed-in patient created the chat booking, matching Calculate().
        var denta = await _db.AppointmentRequests
            .AsNoTracking()
            .CountAsync(a => a.Comment != null && a.Comment.Contains(DentaMarker), cancellationToken);
        var registered = await _db.AppointmentRequests
            .AsNoTracking()
            .CountAsync(a => (a.Comment == null || !a.Comment.Contains(DentaMarker))
                             && a.PatientId > 0,
                cancellationToken);
        var guest = total - denta - registered;

        // CreatedAt is stored in UTC, while dashboard periods are clinic-local.
        // Convert local period boundaries to UTC before filtering in SQL; this also
        // preserves correct behavior across DST transitions without per-row timezone
        // conversion for the full historical table.
        var monthStartLocal = new DateTime(clinicNow.Year, clinicNow.Month, 1);
        var nextMonthLocal = monthStartLocal.AddMonths(1);
        var monthStartUtc = _clock.ToUtc(monthStartLocal);
        var nextMonthUtc = _clock.ToUtc(nextMonthLocal);
        var thisMonth = await _db.AppointmentRequests
            .AsNoTracking()
            .CountAsync(a => a.CreatedAt >= monthStartUtc && a.CreatedAt < nextMonthUtc, cancellationToken);

        // Only the 30-day chart needs individual timestamps. Bound materialization
        // to exactly that clinic-local window, then convert those rows for grouping.
        var dayStartLocal = clinicNow.Date.AddDays(-29);
        var dayEndExclusiveLocal = clinicNow.Date.AddDays(1);
        var dayStartUtc = _clock.ToUtc(dayStartLocal);
        var dayEndExclusiveUtc = _clock.ToUtc(dayEndExclusiveLocal);
        var recentCreatedAtUtc = await _db.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.CreatedAt >= dayStartUtc && a.CreatedAt < dayEndExclusiveUtc)
            .Select(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var dayCounts = Enumerable.Range(0, 30)
            .Select(offset => dayStartLocal.AddDays(offset))
            .ToDictionary(date => date, _ => 0);
        foreach (var createdAtUtc in recentCreatedAtUtc)
        {
            var localDate = _clock.FromUtc(createdAtUtc).Date;
            if (dayCounts.ContainsKey(localDate))
                dayCounts[localDate]++;
        }

        var byDay = dayCounts
            .OrderBy(pair => pair.Key)
            .Select(pair => new AdminAnalyticsDay(pair.Key.ToString("yyyy-MM-dd"), pair.Value))
            .ToArray();

        // Lifetime doctor totals are aggregated in SQL. Materialize one row per
        // doctor rather than one row per appointment, fetch names only for doctors
        // that actually appear in history, then keep the existing count/name sort.
        var doctorCounts = await _db.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.DoctorId.HasValue)
            .GroupBy(a => a.DoctorId!.Value)
            .Select(group => new DoctorCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var doctorIds = doctorCounts.Select(row => row.DoctorId).ToArray();
        var doctorNames = doctorIds.Length == 0
            ? new Dictionary<int, string>()
            : await _db.Doctors
                .AsNoTracking()
                .Where(doctor => doctorIds.Contains(doctor.Id))
                .ToDictionaryAsync(doctor => doctor.Id, doctor => doctor.FullName, cancellationToken);

        var byDoctor = doctorCounts
            .Select(row => new AdminAnalyticsDoctor(
                row.DoctorId,
                doctorNames.TryGetValue(row.DoctorId, out var name) ? name : $"Врач #{row.DoctorId}",
                row.Count))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.DoctorName, StringComparer.CurrentCulture)
            .Take(8)
            .ToArray();

        return new AdminAnalyticsSummary(
            GeneratedAt: DateTime.SpecifyKind(clinicNow, DateTimeKind.Unspecified),
            TotalRequests: total,
            ThisMonthRequests: thisMonth,
            ConfirmedOrCompletedRate: confirmedOrCompletedRate,
            Statuses: new AdminAnalyticsStatuses(pending, confirmed, completed, cancelled, unknown),
            Sources: new AdminAnalyticsSources(registered, guest, denta),
            ByDay: byDay,
            ByDoctor: byDoctor);
    }

    internal static AdminAnalyticsSummary Calculate(
        IReadOnlyCollection<AnalyticsAppointmentRow> rows,
        IReadOnlyDictionary<int, string> doctorNames,
        DateTime clinicNow)
    {
        var total = rows.Count;
        var normalized = rows
            .Select(r => new { Row = r, Status = NormalizeStatus(r.Status) })
            .ToList();

        var pending = normalized.Count(x => x.Status == AppointmentStatuses.Pending);
        var confirmed = normalized.Count(x => x.Status == AppointmentStatuses.Confirmed);
        var completed = normalized.Count(x => x.Status == AppointmentStatuses.Completed);
        var cancelled = normalized.Count(x => x.Status == AppointmentStatuses.Cancelled);
        var unknown = total - pending - confirmed - completed - cancelled;
        var confirmedLike = confirmed + completed;
        var confirmedOrCompletedRate = total == 0
            ? 0
            : Math.Round(confirmedLike * 100d / total, 1, MidpointRounding.AwayFromZero);

        var monthStart = new DateTime(clinicNow.Year, clinicNow.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var thisMonth = rows.Count(r =>
            r.CreatedAt >= monthStart
            && r.CreatedAt < nextMonth);

        var registered = 0;
        var guest = 0;
        var denta = 0;
        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.Comment)
                && row.Comment.Contains(DentaMarker, StringComparison.Ordinal))
            {
                denta++;
            }
            else if (row.PatientId is > 0)
            {
                registered++;
            }
            else
            {
                guest++;
            }
        }

        var dayStart = clinicNow.Date.AddDays(-29);
        var dayCounts = Enumerable.Range(0, 30)
            .Select(offset => dayStart.AddDays(offset))
            .ToDictionary(d => d, _ => 0);

        foreach (var row in rows)
        {
            var date = row.CreatedAt.Date;
            if (dayCounts.ContainsKey(date)) dayCounts[date]++;
        }

        var byDay = dayCounts
            .OrderBy(x => x.Key)
            .Select(x => new AdminAnalyticsDay(x.Key.ToString("yyyy-MM-dd"), x.Value))
            .ToArray();

        var byDoctor = rows
            .Where(r => r.DoctorId.HasValue)
            .GroupBy(r => r.DoctorId!.Value)
            .Select(group => new AdminAnalyticsDoctor(
                group.Key,
                doctorNames.TryGetValue(group.Key, out var name) ? name : $"Врач #{group.Key}",
                group.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.DoctorName, StringComparer.CurrentCulture)
            .Take(8)
            .ToArray();

        return new AdminAnalyticsSummary(
            GeneratedAt: DateTime.SpecifyKind(clinicNow, DateTimeKind.Unspecified),
            TotalRequests: total,
            ThisMonthRequests: thisMonth,
            ConfirmedOrCompletedRate: confirmedOrCompletedRate,
            Statuses: new AdminAnalyticsStatuses(pending, confirmed, completed, cancelled, unknown),
            Sources: new AdminAnalyticsSources(registered, guest, denta),
            ByDay: byDay,
            ByDoctor: byDoctor);
    }

    private static int CountStatus(IEnumerable<StatusCount> groups, string status)
        => groups.Where(group => group.Status == status).Sum(group => group.Count);

    private static string NormalizeStatus(string? status) =>
        AppointmentStatuses.TryNormalize(status, out var normalized) ? normalized : string.Empty;

    private sealed record StatusCount(string Status, int Count);
    private sealed record DoctorCount(int DoctorId, int Count);

    internal sealed record AnalyticsAppointmentRow(
        string? Status,
        DateTime? AppointmentDate,
        int? DoctorId,
        int? PatientId,
        string? Comment,
        DateTime CreatedAt);
}

public sealed record AdminAnalyticsSummary(
    DateTime GeneratedAt,
    int TotalRequests,
    int ThisMonthRequests,
    double ConfirmedOrCompletedRate,
    AdminAnalyticsStatuses Statuses,
    AdminAnalyticsSources Sources,
    IReadOnlyList<AdminAnalyticsDay> ByDay,
    IReadOnlyList<AdminAnalyticsDoctor> ByDoctor);

public sealed record AdminAnalyticsStatuses(
    int Pending,
    int Confirmed,
    int Completed,
    int Cancelled,
    int Unknown);

public sealed record AdminAnalyticsSources(
    int Registered,
    int Guest,
    int Denta);

public sealed record AdminAnalyticsDay(string Date, int Count);

public sealed record AdminAnalyticsDoctor(int DoctorId, string DoctorName, int Count);
