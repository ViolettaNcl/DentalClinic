using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ClinicClockTests
{
    [Fact]
    public void UsesConfiguredClinicTimeZone()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "Europe/Moscow"
            })
            .Build();
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 6, 0, 0, TimeSpan.Zero));

        var clock = new ClinicClock(configuration, provider);

        Assert.Equal("Europe/Moscow", clock.TimeZone.Id);
        Assert.Equal(new DateTime(2026, 9, 2, 9, 0, 0), clock.Now);
    }

    [Fact]
    public void InvalidConfiguredTimeZone_FailsFast()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "Not/A-Real-Time-Zone"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ClinicClock(configuration, new FixedTimeProvider(DateTimeOffset.UtcNow)));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
