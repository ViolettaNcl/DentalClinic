using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Hard limits for compatibility appointment feeds. The legacy dashboards expect
/// arrays rather than a paged envelope, so keep all live work while bounding old
/// history to prevent an authenticated request from materializing the whole table.
/// Proper server-side history pagination can build on top of the same limits.
/// </summary>
public static class AppointmentReadPolicy
{
    public const int PatientActiveLimit = 100;
    public const int PatientHistoryLimit = 200;
    public const int AdminActiveLimit = 250;
    public const int AdminHistoryLimit = 500;

    public static async Task<BoundedRead<T>> ReadAsync<T>(
        IQueryable<T> query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var rows = await query
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > limit;
        if (truncated)
            rows.RemoveAt(rows.Count - 1);

        return new BoundedRead<T>(rows, truncated);
    }
}

public sealed record BoundedRead<T>(IReadOnlyList<T> Items, bool Truncated);
