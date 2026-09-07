namespace DentalClinic.Services;

public static class DentaLinkPolicy
{
    private const int MaxLinks = 2;
    private const int MaxLabelLength = 120;
    private const int MaxUrlLength = 300;

    public static List<Dictionary<string, string>> Filter(
        IEnumerable<Dictionary<string, string>>? links)
    {
        var safe = new List<Dictionary<string, string>>();
        if (links == null) return safe;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            if (safe.Count >= MaxLinks) break;

            if (!link.TryGetValue("text", out var text)
                || !link.TryGetValue("url", out var url))
            {
                continue;
            }

            text = text?.Trim() ?? string.Empty;
            url = url?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text)
                || text.Length > MaxLabelLength
                || !IsAllowedInternalUrl(url)
                || !seen.Add(url))
            {
                continue;
            }

            safe.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = text,
                ["url"] = url
            });
        }

        return safe;
    }

    public static bool IsAllowedInternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || url.Length > MaxUrlLength
            || !url.StartsWith("/pages/", StringComparison.Ordinal)
            || url.StartsWith("//", StringComparison.Ordinal)
            || url.Contains('\\'))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(url);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decoded.Contains("..", StringComparison.Ordinal)
            || decoded.Any(char.IsControl))
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Relative, out _);
    }
}
