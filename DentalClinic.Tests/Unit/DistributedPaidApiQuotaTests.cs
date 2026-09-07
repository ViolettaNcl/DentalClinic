using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DistributedRequestQuotaTests
{
    [Fact]
    public async Task TryAcquire_EnforcesLimitAndResetsOnNextMinute()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"request-quota-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 6, 18, 20, 30, TimeSpan.Zero));
        var quota = new DistributedRequestQuotaService(db, time);
        var clientKey = RateLimitClientKey.Create("203.0.113.42");

        Assert.True(await quota.TryAcquireAsync("tts", clientKey, 2));
        Assert.True(await quota.TryAcquireAsync("tts", clientKey, 2));
        Assert.False(await quota.TryAcquireAsync("tts", clientKey, 2));

        var row = await db.PaidApiUsageWindows.SingleAsync();
        Assert.Equal(2, row.RequestCount);
        Assert.Equal(new DateTime(2026, 9, 6, 18, 20, 0, DateTimeKind.Utc), row.WindowStartUtc);

        time.SetUtcNow(new DateTimeOffset(2026, 9, 6, 18, 21, 1, TimeSpan.Zero));
        Assert.True(await quota.TryAcquireAsync("tts", clientKey, 2));

        Assert.Equal(1, row.RequestCount);
        Assert.Equal(new DateTime(2026, 9, 6, 18, 21, 0, DateTimeKind.Utc), row.WindowStartUtc);
    }

    [Fact]
    public async Task TryAcquire_KeepsBucketsIndependentForSameClient()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"request-quota-buckets-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var quota = new DistributedRequestQuotaService(
            db,
            new MutableTimeProvider(new DateTimeOffset(2026, 9, 7, 8, 10, 5, TimeSpan.Zero)));
        var clientKey = RateLimitClientKey.Create("198.51.100.10");

        Assert.True(await quota.TryAcquireAsync("auth", clientKey, 1));
        Assert.False(await quota.TryAcquireAsync("auth", clientKey, 1));
        Assert.True(await quota.TryAcquireAsync("appointment-create", clientKey, 1));

        Assert.Equal(2, await db.PaidApiUsageWindows.CountAsync());
    }

    [Theory]
    [InlineData("/api/chat", "chat", ChatRateLimitPolicy.ChatPermitLimit)]
    [InlineData("/api/chat/stream", "chat", ChatRateLimitPolicy.ChatPermitLimit)]
    [InlineData("/api/chat/tts", "tts", ChatRateLimitPolicy.TtsPermitLimit)]
    [InlineData("/api/translate", "translate", PaidApiQuotaPolicy.TranslatePermitLimit)]
    [InlineData("/api/review/translate", "translate", PaidApiQuotaPolicy.TranslatePermitLimit)]
    public void PaidPolicy_MapsEveryPaidRouteToSharedProductionBudget(string path, string bucket, int limit)
    {
        Assert.True(PaidApiQuotaPolicy.TryResolve(path, out var profile));
        Assert.Equal(bucket, profile.Bucket);
        Assert.Equal(limit, profile.PermitLimit);
    }

    [Theory]
    [InlineData("/api/appointmentrequest", "appointment-create", GeneralRateLimitPolicy.AppointmentCreatePermitLimit)]
    [InlineData("/api/auth/register", "auth", GeneralRateLimitPolicy.AuthPermitLimit)]
    [InlineData("/api/auth/login", "auth", GeneralRateLimitPolicy.AuthPermitLimit)]
    [InlineData("/API/AUTH/LOGIN", "auth", GeneralRateLimitPolicy.AuthPermitLimit)]
    public void GeneralPolicy_MapsProtectedPostRoutesToSharedProductionBudget(string path, string bucket, int limit)
    {
        Assert.True(GeneralRateLimitPolicy.TryResolve("POST", path, out var profile));
        Assert.Equal(bucket, profile.Bucket);
        Assert.Equal(limit, profile.PermitLimit);
    }

    [Theory]
    [InlineData("GET", "/api/auth/login")]
    [InlineData("OPTIONS", "/api/auth/register")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("POST", "/api/appointmentrequest/patient/1")]
    public void GeneralPolicy_IgnoresMethodsAndRoutesOutsideProtectedWrites(string method, string path)
    {
        Assert.False(GeneralRateLimitPolicy.TryResolve(method, path, out _));
    }

    [Fact]
    public void ClientKey_DoesNotPersistRawClientAddress()
    {
        const string ip = "203.0.113.42";

        var first = RateLimitClientKey.Create(ip);
        var second = RateLimitClientKey.Create(ip);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(ip, first, StringComparison.Ordinal);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.Equal(first, PaidApiQuotaPolicy.CreateClientKey(ip));
    }

    [Fact]
    public void PaidPolicy_IgnoresGeneralRoutes()
    {
        Assert.False(PaidApiQuotaPolicy.TryResolve("/api/auth/login", out _));
        Assert.False(PaidApiQuotaPolicy.TryResolve("/api/appointmentrequest", out _));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}
