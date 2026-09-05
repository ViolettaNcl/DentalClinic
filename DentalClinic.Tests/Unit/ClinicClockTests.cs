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
    public void ConvertsClinicLocalAndUtcWithoutUsingServerLocalZone()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "Europe/Moscow"
            })
            .Build();
        var clock = new ClinicClock(
            configuration,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 6, 0, 0, TimeSpan.Zero)));

        var clinicMidnight = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Unspecified);
        var utc = clock.ToUtc(clinicMidnight);
        var clinic = clock.FromUtc(utc);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 9, 1, 21, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Unspecified, clinic.Kind);
        Assert.Equal(clinicMidnight, clinic);
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
