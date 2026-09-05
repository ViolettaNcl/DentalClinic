using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ServiceCatalogPolicyTests
{
    [Theory]
    [InlineData(0, null, true)]
    [InlineData(100, 100, true)]
    [InlineData(100, 250, true)]
    [InlineData(-1, null, false)]
    [InlineData(100, 99, false)]
    public void PriceRange_ValidatesExpectedOrder(double from, double? to, bool expected)
    {
        Assert.Equal(expected, ServiceCatalogPolicy.IsValidPriceRange((decimal)from, to.HasValue ? (decimal)to.Value : null));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/pages/services/implants.html", true)]
    [InlineData("https://example.com/service", false)]
    [InlineData("//example.com/service", false)]
    [InlineData("/pages/../secret", false)]
    [InlineData("/pages\\secret", false)]
    [InlineData("/other/services.html", false)]
    public void PageUrl_AllowsOnlySafeLocalPages(string? url, bool expected)
    {
        Assert.Equal(expected, ServiceCatalogPolicy.IsValidPageUrl(url));
    }
}
