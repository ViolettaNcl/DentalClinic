using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

/// <summary>
/// One rolling fixed-window counter per paid API bucket and pseudonymous client.
/// The row is reused every minute so the table stays bounded by active clients,
/// rather than growing by one row per request/window.
/// </summary>
public sealed class PaidApiUsageWindow
{
    [StringLength(32)]
    public required string Bucket { get; set; }

    [StringLength(64)]
    public required string ClientKey { get; set; }

    public DateTime WindowStartUtc { get; set; }
    public int RequestCount { get; set; }
}