namespace DentalClinic.Services;

public readonly record struct GeneralRateLimitProfile(string Bucket, int PermitLimit);

/// <summary>
/// Production distributed quotas for non-paid write endpoints that are also protected
/// by ASP.NET Core's process-local limiter. Keep these values aligned with Program.cs.
/// </summary>
public static class GeneralRateLimitPolicy
{
    public const int AppointmentCreatePermitLimit = 3;
    public const int AuthPermitLimit = 8;

    public static bool TryResolve(string? method, string? path, out GeneralRateLimitProfile profile)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            profile = default;
            return false;
        }

        if (string.Equals(path, "/api/appointmentrequest", StringComparison.OrdinalIgnoreCase))
        {
            profile = new GeneralRateLimitProfile("appointment-create", AppointmentCreatePermitLimit);
            return true;
        }

        if (string.Equals(path, "/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            profile = new GeneralRateLimitProfile("auth", AuthPermitLimit);
            return true;
        }

        profile = default;
        return false;
    }
}
