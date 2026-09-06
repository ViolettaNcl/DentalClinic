namespace DentalClinic.Services;

public static class PaidApiRoutePolicy
{
    public static bool RequiresSameOrigin(string? method, string? path)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(path, "/api/chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/chat/stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/chat/tts", StringComparison.OrdinalIgnoreCase);
    }
}
