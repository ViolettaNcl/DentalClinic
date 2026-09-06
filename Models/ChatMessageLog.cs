using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace DentalClinic.Models;

[Table("ChatMessageLogs")]
public class ChatMessageLog
{
    private static readonly HashSet<string> SupportedLanguages =
        new(StringComparer.Ordinal) { "ru", "en", "fr", "el", "ar" };

    private string? _clientIp;
    private string _sessionId = string.Empty;
    private string _lang = "ru";

    [Key]
    public int Id { get; set; }

    // SessionId comes from the browser and is therefore untrusted. The database
    // column is nvarchar(64); normalize unusual/oversized values to a stable hash
    // so analytics logging cannot be disabled by submitting a too-long identifier.
    [Required, StringLength(64)]
    public string SessionId
    {
        get => _sessionId;
        set => _sessionId = NormalizeSessionId(value);
    }

    public int? PatientId { get; set; }

    [Required, StringLength(10)]
    public string Role { get; set; } = "user";

    [Required, StringLength(1000)]
    public string Text { get; set; } = "";

    // The request language is client-controlled as well. Keep analytics rows inside
    // the five supported locale codes and inside the nvarchar(5) database contract.
    [StringLength(5)]
    public string Lang
    {
        get => _lang;
        set => _lang = NormalizeLanguage(value);
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Stage 3 privacy: raw IP addresses are never persisted. The setter accepts
    // the request IP for backwards-compatible controller code and immediately
    // replaces it with a fixed-length SHA-256 pseudonym. A retention cleanup
    // clears even this pseudonym after a short configurable period.
    [StringLength(64)]
    public string? ClientIp
    {
        get => _clientIp;
        set => _clientIp = NormalizeClientIp(value);
    }

    private static string NormalizeSessionId(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return string.Empty;

        // Standard client ids are UUID/hex-like values. Preserve them verbatim so
        // existing analytics grouping stays readable and backwards compatible.
        if (normalized.Length <= 64
            && normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return normalized;
        }

        // Hash instead of truncating: two malicious ids with the same 64-character
        // prefix must not collapse into one analytics session.
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
    }

    private static string NormalizeLanguage(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return SupportedLanguages.Contains(normalized) ? normalized : "ru";
    }

    private static string? NormalizeClientIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Already-pseudonymized values loaded by EF must remain unchanged.
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
            return value.ToLowerInvariant();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
