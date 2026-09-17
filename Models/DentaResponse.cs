using System.Text.Json.Serialization;

namespace DentalClinic.Models;

public sealed class DentaResponse
{
    public string Reply { get; set; } = "";
    public List<string> Suggestions { get; set; } = new();
    public List<DentaLink> Links { get; set; } = new();
    public bool StartBooking { get; set; }

    // Internal provenance for logs/tests. Never serialized to the browser.
    [JsonIgnore]
    public string Source { get; set; } = "ai";
}

public sealed class DentaLink
{
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed record DentaTurn(string Role, string Text);
