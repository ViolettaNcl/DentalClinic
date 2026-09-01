namespace DentalClinic.Models;

public static class AppointmentStatuses
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Confirmed,
        Cancelled,
        Completed
    };

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return Allowed.Contains(normalized);
    }

    public static bool CanTransition(string? current, string next)
    {
        if (!TryNormalize(current, out var from) || !TryNormalize(next, out var to))
            return false;

        if (from == to) return true;

        return from switch
        {
            Pending => to is Confirmed or Cancelled,
            Confirmed => to is Completed or Cancelled,
            Cancelled => to is Pending,
            Completed => false,
            _ => false
        };
    }

    public static bool BlocksDoctorSlot(string? status) =>
        TryNormalize(status, out var normalized) && normalized is Pending or Confirmed;
}
