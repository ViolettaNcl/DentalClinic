using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DentalClinic.Models;
using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ServiceCatalogPolicyTests
{
    [Theory]
    [InlineData(0.0, null, true)]
    [InlineData(100.0, 100.0, true)]
    [InlineData(100.0, 250.0, true)]
    [InlineData(-1.0, null, false)]
    [InlineData(100.0, 99.0, false)]
    public void PriceRange_ValidatesExpectedOrder(double from, double? to, bool expected)
    {
        Assert.Equal(expected, ServiceCatalogPolicy.IsValidPriceRange((decimal)from, to.HasValue ? (decimal)to.Value : null));
    }

    [Fact]
    public void PriceRange_AllowsExactDecimal10_2Maximum()
    {
        Assert.True(ServiceCatalogPolicy.IsValidPriceRange(
            ServiceCatalogPolicy.MaxPersistedPrice,
            ServiceCatalogPolicy.MaxPersistedPrice));
    }

    [Fact]
    public void PriceRange_RejectsPriceFromBeyondDecimal10_2Maximum()
    {
        Assert.False(ServiceCatalogPolicy.IsValidPriceRange(100_000_000m, null));
    }

    [Fact]
    public void PriceRange_RejectsPriceToBeyondDecimal10_2Maximum()
    {
        Assert.False(ServiceCatalogPolicy.IsValidPriceRange(100m, 100_000_000m));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    public void SortOrder_AllowsOnlyNonNegativeValues(int sortOrder, bool expected)
    {
        Assert.Equal(expected, ServiceCatalogPolicy.IsValidSortOrder(sortOrder));
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

    [Theory]
    [InlineData(nameof(UpdateServiceRequest.Category), 100)]
    [InlineData(nameof(UpdateServiceRequest.Name), 200)]
    [InlineData(nameof(UpdateServiceRequest.Description), 500)]
    [InlineData(nameof(UpdateServiceRequest.Unit), 30)]
    [InlineData(nameof(UpdateServiceRequest.Keywords), 300)]
    [InlineData(nameof(UpdateServiceRequest.PageUrl), 300)]
    public void UpdateRequest_StringLengthBoundsMatchServicePersistence(string propertyName, int expectedMaximum)
    {
        var property = typeof(UpdateServiceRequest).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);

        var attribute = property!.GetCustomAttribute<StringLengthAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedMaximum, attribute!.MaximumLength);
    }
}
