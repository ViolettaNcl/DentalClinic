using System.Security.Cryptography;
using System.Text;

namespace DentalClinic.Services;

public readonly record struct PaidApiQuotaProfile(string Bucket, int PermitLimit);

public static class PaidApiQuotaPolicy
{
    public const int TranslatePermitLimit = 40;

    public static bool TryResolve(string? path, out PaidApiQuotaProfile profile)
    {
        if (string.Equals(path, "/api/chat/tts", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("tts", ChatRateLimitPolicy.TtsPermitLimit);
            return true;
        }

        if (string.Equals(path, "/api/chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/chat/stream", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("chat", ChatRateLimitPolicy.ChatPermitLimit);
            return true;
        }

        if (string.Equals(path, "/api/translate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/review/translate", StringComparison.OrdinalIgnoreCase))
        {
            profile = new PaidApiQuotaProfile("translate", TranslatePermitLimit);
            return true;
        }

        profile = default;
        return false;
    }

    public static string CreateClientKey(string? remoteAddress)
    {
        var normalized = string.IsNullOrWhiteSpace(remoteAddress)
            ? "unknown"
            : remoteAddress.Trim();

        // Never persist raw IP addresses in quota rows. The fixed-length digest is
        // sufficient for rate-limit partitioning and follows the chat-log privacy
        // model already used elsewhere in the application.
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
    }
}