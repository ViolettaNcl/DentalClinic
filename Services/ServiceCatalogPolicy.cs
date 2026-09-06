namespace DentalClinic.Services;

/// <summary>
/// Validation rules shared by the admin service catalogue and Denta knowledge
/// source. Service links are intentionally restricted to local clinic pages so an
/// admin typo cannot turn a trusted service CTA into an external redirect.
/// </summary>
public static class ServiceCatalogPolicy
{
    public static bool IsValidPriceRange(decimal priceFrom, decimal? priceTo) =>
        priceFrom >= 0 && (!priceTo.HasValue || priceTo.Value >= priceFrom);

    public static bool IsValidPageUrl(string? pageUrl)
    {
        if (string.IsNullOrWhiteSpace(pageUrl)) return true;

        var value = pageUrl.Trim();
        return value.StartsWith("/pages/", StringComparison.Ordinal)
               && !value.Contains("..", StringComparison.Ordinal)
               && !value.Contains('\\')
               && !value.Contains("//", StringComparison.Ordinal);
    }
}
