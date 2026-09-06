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
        if (string.IsNullOrWhiteSpace(origin))
        {
            if (string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase))
                return true;

            return allowDirectRequests;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            return false;

        var expectedPort = requestPort
            ?? (string.Equals(requestScheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);

        return string.Equals(originUri.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(originUri.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            && originUri.Port == expectedPort;
    }
}
