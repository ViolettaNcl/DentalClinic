namespace DentalClinic.Services;

public static class UnsafeRequestOriginPolicy
{
    private static readonly HashSet<string> SessionMutationPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/register",
        "/api/auth/login",
        "/api/auth/admin/login",
        "/api/auth/logout"
    };

    public static bool RequiresValidation(string? method, string? path, bool hasAuthCookie)
    {
        if (!IsUnsafeMethod(method) || string.IsNullOrWhiteSpace(path))
            return false;

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        return hasAuthCookie || SessionMutationPaths.Contains(path);
    }

    public static bool IsAllowed(
        string? origin,
        string? referer,
        string? fetchSite,
        string requestScheme,
        string requestHost,
        int? requestPort,
        bool allowDirectRequests)
    {
        // Fetch Metadata is only a supporting browser signal. A forged "same-origin"
        // value must never turn a request without Origin/Referer into an allowed
        // production request. A genuine cross-site signal, however, is sufficient
        // reason to reject immediately.
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(origin))
            return MatchesRequestOrigin(origin, requestScheme, requestHost, requestPort);

        if (!string.IsNullOrWhiteSpace(referer))
            return MatchesRequestOrigin(referer, requestScheme, requestHost, requestPort);

        return allowDirectRequests;
    }

    private static bool IsUnsafeMethod(string? method)
        => string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesRequestOrigin(
        string value,
        string requestScheme,
        string requestHost,
        int? requestPort)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        var expectedPort = requestPort
            ?? (string.Equals(requestScheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);

        return string.Equals(uri.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            && uri.Port == expectedPort;
    }
}
