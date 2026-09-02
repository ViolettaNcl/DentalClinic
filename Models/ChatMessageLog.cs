using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace DentalClinic.Models;

[Table("ChatMessageLogs")]
public class ChatMessageLog
{
    private string? _clientIp;

    [Key]
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string SessionId { get; set; } = null!;

    public int? PatientId { get; set; }

    [Required, StringLength(10)]
    public string Role { get; set; } = "user";

    [Required, StringLength(1000)]
    public string Text { get; set; } = "";

    [StringLength(5)]
    public string Lang { get; set; } = "ru";

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

    private static string? NormalizeClientIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Already-pseudonymized values loaded by EF must remain unchanged.
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
            return value.ToLowerInvariant();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
