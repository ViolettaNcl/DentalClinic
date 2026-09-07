namespace DentalClinic.Services;

public readonly record struct PaidApiQuotaProfile(string Bucket, int PermitLimit);

public static class PaidApiQuotaPolicy
{
    public const int TranslatePermitLimit = 40;

    public static bool TryResolve(string? path, out PaidApiQuotaProfile profile)
    {
        var normalizedPath = ApiRoutePath.Normalize(path);

        if (string.Equals(normalizedPath, "/api/chat/tts", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("tts", ChatRateLimitPolicy.TtsPermitLimit);
            return true;
        }

        if (string.Equals(normalizedPath, "/api/chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/chat/stream", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("chat", ChatRateLimitPolicy.ChatPermitLimit);
            return true;
        }

        if (string.Equals(normalizedPath, "/api/translate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/review/translate", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("translate", TranslatePermitLimit);
            return true;
        }

        profile = default;
        return false;
    }

    public static string CreateClientKey(string? remoteAddress)
        => RateLimitClientKey.Create(remoteAddress);
}
