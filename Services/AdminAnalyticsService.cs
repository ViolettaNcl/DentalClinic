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
        var rows = await _db.AppointmentRequests
            .AsNoTracking()
            .Select(a => new AnalyticsAppointmentRow(
                a.Status,
                a.AppointmentDate,
                a.DoctorId,
                a.PatientId,
                a.Comment))
            .ToListAsync(cancellationToken);

        var doctorNames = await _db.Doctors
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FullName, cancellationToken);

        return Calculate(rows, doctorNames, _clock.Now);
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
        var thisMonth = rows.Count(r => r.AppointmentDate is >= var d && d >= monthStart && d < nextMonth);

        var registered = 0;
        var guest = 0;
        var denta = 0;
        foreach (var row in rows)
        {
            // Sources are deliberately mutually exclusive. Denta wins even when
            // a signed-in patient created the chat booking, so chart totals always
            // add up exactly to TotalRequests.
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
            if (!row.AppointmentDate.HasValue) continue;
            var date = row.AppointmentDate.Value.Date;
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

    private static string NormalizeStatus(string? status) =>
        AppointmentStatuses.TryNormalize(status, out var normalized) ? normalized : string.Empty;

    internal sealed record AnalyticsAppointmentRow(
        string? Status,
        DateTime? AppointmentDate,
        int? DoctorId,
        int? PatientId,
        string? Comment);
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
