using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

/// <summary>
/// One fixed-window counter per quota bucket and pseudonymous client. The historical
/// type/table name is retained to avoid a production schema rename; the same bounded
/// rows now back both paid-provider and general distributed request quotas.
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
