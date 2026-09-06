using System.Collections.Concurrent;
using System.Data;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Database-backed fixed-window quota for paid external-provider endpoints.
/// ASP.NET's built-in rate limiter is process-local; this layer makes the same
/// per-client budget hold across multiple Vercel/container instances.
/// </summary>
public sealed class DistributedPaidApiQuotaService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InMemoryGates = new();

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public DistributedPaidApiQuotaService(ApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<bool> TryAcquireAsync(
        string bucket,
        string clientKey,
        int permitLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Quota bucket is required", nameof(bucket));
        if (string.IsNullOrWhiteSpace(clientKey))
            throw new ArgumentException("Quota client key is required", nameof(clientKey));
        if (permitLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(permitLimit));

        var windowStartUtc = FloorToMinute(_timeProvider.GetUtcNow().UtcDateTime);

        if (!_db.Database.IsRelational())
        {
            var gateKey = $"{bucket}:{clientKey}";
            var gate = InMemoryGates.GetOrAdd(gateKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await AcquireRowAsync(bucket, clientKey, permitLimit, windowStartUtc, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        if (string.Equals(_db.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            // SQL Server application locks serialize the exact client+bucket row even
            // before it exists. That closes the cold-start race where two instances
            // could both observe a missing counter and insert/allow independently.
            var resource = $"dental-paid-api:{bucket}:{clientKey}";
            await _db.Database.ExecuteSqlInterpolatedAsync($$"""
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource={{resource}},
    @LockMode='Exclusive',
    @LockOwner='Transaction',
    @LockTimeout=5000;
IF @lockResult < 0
    THROW 51000, 'Unable to acquire paid API quota lock', 1;
""", cancellationToken);

            var allowed = await AcquireRowAsync(
                bucket,
                clientKey,
                permitLimit,
                windowStartUtc,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return allowed;
        }

        // The production provider is SQL Server. Keep a safe relational fallback for
        // alternate providers used by operators/tests: serializable isolation protects
        // the read/update window as strongly as the provider supports.
        await using (var transaction = await _db.Database.BeginTransactionAsync(
                         IsolationLevel.Serializable,
                         cancellationToken))
        {
            var allowed = await AcquireRowAsync(
                bucket,
                clientKey,
                permitLimit,
                windowStartUtc,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return allowed;
        }
    }

    private async Task<bool> AcquireRowAsync(
        string bucket,
        string clientKey,
        int permitLimit,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
    {
        var row = await _db.PaidApiUsageWindows
            .SingleOrDefaultAsync(
                x => x.Bucket == bucket && x.ClientKey == clientKey,
                cancellationToken);

        if (row == null)
        {
            _db.PaidApiUsageWindows.Add(new PaidApiUsageWindow
            {
                Bucket = bucket,
                ClientKey = clientKey,
                WindowStartUtc = windowStartUtc,
                RequestCount = 1
            });
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (row.WindowStartUtc < windowStartUtc)
        {
            row.WindowStartUtc = windowStartUtc;
            row.RequestCount = 1;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        // If the stored window is unexpectedly in the future, fail closed instead of
        // resetting the quota and creating an unlimited path during clock anomalies.
        if (row.WindowStartUtc > windowStartUtc || row.RequestCount >= permitLimit)
            return false;

        row.RequestCount = Math.Max(0, row.RequestCount) + 1;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static DateTime FloorToMinute(DateTime utc)
        => new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
}