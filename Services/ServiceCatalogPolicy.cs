namespace DentalClinic.Services;

/// <summary>
/// Validation rules shared by the admin service catalogue and Denta knowledge
/// source. Service links are intentionally restricted to local clinic pages so an
/// admin typo cannot turn a trusted service CTA into an external redirect.
/// </summary>
public static class ServiceCatalogPolicy
{
    // SQL persistence uses decimal(10,2), so values above this cannot be stored
    // safely even if they pass the business checks for non-negative price/order.
    public const decimal MaxPersistedPrice = 99_999_999.99m;

    public static bool IsValidPriceRange(decimal priceFrom, decimal? priceTo) =>
        priceFrom >= 0
        && priceFrom <= MaxPersistedPrice
        && (!priceTo.HasValue
            || (priceTo.Value >= priceFrom && priceTo.Value <= MaxPersistedPrice));

    public static bool IsValidSortOrder(int sortOrder) => sortOrder >= 0;

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
