using System.Security.Cryptography;
using System.Text;

namespace DentalClinic.Services;

/// <summary>
/// Produces a stable pseudonymous partition key for request quotas without persisting
/// the caller's raw network address in the database.
/// </summary>
public static class RateLimitClientKey
{
    public static string Create(string? remoteAddress)
    {
        var normalized = string.IsNullOrWhiteSpace(remoteAddress)
            ? "unknown"
            : remoteAddress.Trim();

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
    }
}
