using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DistributedPaidApiQuotaTests
{
    [Fact]
    public async Task TryAcquire_EnforcesLimitAndResetsOnNextMinute()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"paid-quota-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 6, 18, 20, 30, TimeSpan.Zero));
        var quota = new DistributedPaidApiQuotaService(db, time);
        var clientKey = PaidApiQuotaPolicy.CreateClientKey("203.0.113.42");

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

    [Theory]
    [InlineData("/api/chat", "chat", ChatRateLimitPolicy.ChatPermitLimit)]
    [InlineData("/api/chat/stream", "chat", ChatRateLimitPolicy.ChatPermitLimit)]
    [InlineData("/api/chat/tts", "tts", ChatRateLimitPolicy.TtsPermitLimit)]
    [InlineData("/api/translate", "translate", PaidApiQuotaPolicy.TranslatePermitLimit)]
    [InlineData("/api/review/translate", "translate", PaidApiQuotaPolicy.TranslatePermitLimit)]
    public void Policy_MapsEveryPaidRouteToSharedProductionBudget(string path, string bucket, int limit)
    {
        Assert.True(PaidApiQuotaPolicy.TryResolve(path, out var profile));
        Assert.Equal(bucket, profile.Bucket);
        Assert.Equal(limit, profile.PermitLimit);
    }

    [Fact]
    public void Policy_DoesNotPersistRawClientAddress()
    {
        const string ip = "203.0.113.42";

        var first = PaidApiQuotaPolicy.CreateClientKey(ip);
        var second = PaidApiQuotaPolicy.CreateClientKey(ip);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(ip, first, StringComparison.Ordinal);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void Policy_IgnoresNonPaidRoutes()
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