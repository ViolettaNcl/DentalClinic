namespace DentalClinic.Services;

/// <summary>
/// Request-size boundary for public endpoints that can spend paid AI-provider quota.
/// The controller-level text clamps run after JSON model binding, so this earlier
/// transport boundary prevents oversized/chunked request bodies from consuming
/// memory before those clamps can run.
/// </summary>
public static class PaidApiPayloadPolicy
{
    public const long MaxRequestBodyBytes = 64 * 1024;

    public static bool IsKnownLengthTooLarge(long? contentLength)
        => contentLength.HasValue && contentLength.Value > MaxRequestBodyBytes;
}
