namespace DentalClinic.Services;

/// <summary>
/// Единый источник локального времени клиники. AppointmentDate хранится как
/// локальное время без смещения, а CreatedAt — в UTC.
/// </summary>
public sealed class ClinicClock
{
    private readonly TimeProvider _timeProvider;

    public ClinicClock(IConfiguration configuration, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        var timeZoneId = configuration["Scheduling:TimeZoneId"] ?? "Europe/Moscow";

        try
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException($"Не найден часовой пояс клиники '{timeZoneId}'", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException($"Некорректный часовой пояс клиники '{timeZoneId}'", ex);
        }
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTime Now => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZone).DateTime;

    public DateTime Normalize(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone), DateTimeKind.Unspecified);

        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }
}
