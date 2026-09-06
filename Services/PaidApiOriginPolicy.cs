namespace DentalClinic.Services;

public static class PaidApiOriginPolicy
{
    public static bool IsAllowed(
        string? origin,
        string? fetchSite,
        string requestScheme,
        string requestHost,
        int? requestPort,
        bool allowDirectRequests)
    {
        // Fetch-Metadata headers are useful browser signals, but they are ordinary
        // client-controlled HTTP headers and can be forged by curl/bots. For paid
        // production APIs, missing Origin therefore cannot be treated as proof of
        // same-origin. Direct calls without Origin stay available only in local/test.
        if (string.IsNullOrWhiteSpace(origin))
            return allowDirectRequests;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            return false;

        var expectedPort = requestPort
            ?? (string.Equals(requestScheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);

        return string.Equals(originUri.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(originUri.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            && originUri.Port == expectedPort;
    }
}
