namespace DentalClinic.Services;

public static class PaidApiRoutePolicy
{
    public static bool RequiresSameOrigin(string? method, string? path)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalizedPath = ApiRoutePath.Normalize(path);

        return string.Equals(normalizedPath, "/api/chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/chat/stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/chat/tts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/translate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/api/review/translate", StringComparison.OrdinalIgnoreCase);
    }
}
