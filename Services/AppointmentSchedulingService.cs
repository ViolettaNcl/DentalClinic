using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

public sealed record SchedulingValidationResult(bool IsValid, string? Message = null, int StatusCode = 400)
{
    public static SchedulingValidationResult Success { get; } = new(true);
    public static SchedulingValidationResult BadRequest(string message) => new(false, message);
    public static SchedulingValidationResult Conflict(string message) => new(false, message, StatusCodes.Status409Conflict);
}

/// <summary>
/// Централизованная проверка времени приёма для всех путей создания и переноса.
/// </summary>
public sealed class AppointmentSchedulingService
{
    private static readonly IReadOnlyDictionary<DayOfWeek, (TimeOnly Open, TimeOnly Close)> DefaultHours =
        new Dictionary<DayOfWeek, (TimeOnly, TimeOnly)>
        {
            [DayOfWeek.Monday] = (new(9, 0), new(20, 0)),
            [DayOfWeek.Tuesday] = (new(9, 0), new(20, 0)),
            [DayOfWeek.Wednesday] = (new(9, 0), new(20, 0)),
            [DayOfWeek.Thursday] = (new(9, 0), new(20, 0)),
            [DayOfWeek.Friday] = (new(9, 0), new(20, 0)),
            [DayOfWeek.Saturday] = (new(9, 0), new(20, 0))
        };

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ClinicClock _clock;

    public AppointmentSchedulingService(
        ApplicationDbContext db,
        IConfiguration configuration,
        ClinicClock clock)
    {
        _db = db;
        _configuration = configuration;
        _clock = clock;
    }

    public DateTime Normalize(DateTime value) => _clock.Normalize(value);

    public async Task<SchedulingValidationResult> ValidateAsync(
        DateTime appointmentDate,
        int? doctorId,
        int? excludeAppointmentId = null,
        bool allowDateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var start = Normalize(appointmentDate);
        var minimumLeadMinutes = Math.Max(0, _configuration.GetValue<int?>("Scheduling:MinimumLeadMinutes") ?? 30);

        // Публичная форма собирает только желаемую дату, а не точное время.
        // В БД такой запрос хранится с 00:00 до назначения слота администратором.
        if (allowDateOnly && !doctorId.HasValue && start.TimeOfDay == TimeSpan.Zero)
        {
            if (start.Date <= _clock.Now.Date)
                return SchedulingValidationResult.BadRequest("Выберите будущую дату приёма");

            return TryGetWorkingHours(start.DayOfWeek, out _, out _)
                ? SchedulingValidationResult.Success
                : SchedulingValidationResult.BadRequest("В выбранный день клиника не работает");
        }

        if (start <= _clock.Now.AddMinutes(minimumLeadMinutes))
            return SchedulingValidationResult.BadRequest("Выберите будущее время приёма");

        var durationMinutes = Math.Max(1, _configuration.GetValue<int?>("Scheduling:AppointmentDurationMinutes") ?? 60);
        var slotIntervalMinutes = Math.Max(1, _configuration.GetValue<int?>("Scheduling:SlotIntervalMinutes") ?? 30);

        if (start.Minute % slotIntervalMinutes != 0 || start.Second != 0 || start.Millisecond != 0)
            return SchedulingValidationResult.BadRequest($"Время приёма должно начинаться с шагом {slotIntervalMinutes} минут");

        if (!TryGetWorkingHours(start.DayOfWeek, out var open, out var close))
            return SchedulingValidationResult.BadRequest("В выбранный день клиника не работает");

        var startTime = TimeOnly.FromDateTime(start);
        var end = start.AddMinutes(durationMinutes);
        var crossesMidnight = end.Date != start.Date;
        var endTime = TimeOnly.FromDateTime(end);
        if (startTime < open || startTime >= close || crossesMidnight || endTime > close)
            return SchedulingValidationResult.BadRequest(
                $"Приём должен полностью помещаться в рабочее время клиники: {open:HH:mm}–{close:HH:mm}");

        if (!doctorId.HasValue)
            return SchedulingValidationResult.Success;

        var doctorExists = await _db.Doctors
            .AnyAsync(d => d.Id == doctorId.Value && d.IsActive, cancellationToken);
        if (!doctorExists)
            return SchedulingValidationResult.BadRequest("Указан несуществующий или неактивный врач");

        var requestedEnd = start.AddMinutes(durationMinutes);
        var earliestConflictingStart = start.AddMinutes(-durationMinutes);

        var conflict = await _db.AppointmentRequests.AnyAsync(a =>
            a.DoctorId == doctorId.Value
            && a.AppointmentDate != null
            && a.AppointmentDate > earliestConflictingStart
            && a.AppointmentDate < requestedEnd
            && (a.Status == AppointmentStatuses.Pending || a.Status == AppointmentStatuses.Confirmed)
            && (!excludeAppointmentId.HasValue || a.Id != excludeAppointmentId.Value),
            cancellationToken);

        return conflict
            ? SchedulingValidationResult.Conflict("Это время уже занято у выбранного врача. Выберите другой слот")
            : SchedulingValidationResult.Success;
    }

    private bool TryGetWorkingHours(DayOfWeek day, out TimeOnly open, out TimeOnly close)
    {
        open = default;
        close = default;

        var section = _configuration.GetSection($"Scheduling:WorkingHours:{day}");
        if (section.Exists())
        {
            if (section.GetValue<bool?>("Closed") == true)
                return false;

            return TimeOnly.TryParse(section["Open"], out open)
                && TimeOnly.TryParse(section["Close"], out close)
                && close > open;
        }

        if (!DefaultHours.TryGetValue(day, out var hours))
            return false;

        (open, close) = hours;
        return true;
    }
}
