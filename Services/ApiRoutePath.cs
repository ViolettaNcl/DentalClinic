namespace DentalClinic.Services;

/// <summary>
/// Normalizes request paths for security/rate-limit policy matching. ASP.NET routing
/// can treat a trailing slash as equivalent, so policy gates must do the same.
/// </summary>
public static class ApiRoutePath
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var normalized = path.Trim();
        while (normalized.Length > 1 && normalized.EndsWith('/'))
            normalized = normalized[..^1];

        return normalized;
    }
}
