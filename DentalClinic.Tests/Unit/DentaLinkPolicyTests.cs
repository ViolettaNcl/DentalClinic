using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaLinkPolicyTests
{
    [Theory]
    [InlineData("/pages/contact.html")]
    [InlineData("/pages/doctors.html")]
    [InlineData("/pages/services/implants.html")]
    public void KnownInternalPageUrls_AreAllowed(string url)
    {
        Assert.True(DentaLinkPolicy.IsAllowedInternalUrl(url));
    }

    [Theory]
    [InlineData("https://external.example/path")]
    [InlineData("//external.example/path")]
    [InlineData("/pages/../admin")]
    [InlineData("/pages/%2e%2e/admin")]
    [InlineData("/pages\\external")]
    public void ExternalOrTraversalUrls_AreRejected(string url)
    {
        Assert.False(DentaLinkPolicy.IsAllowedInternalUrl(url));
    }

    [Fact]
    public void ScriptScheme_IsRejected()
    {
        var url = string.Concat("java", "script", ":void(0)");
        Assert.False(DentaLinkPolicy.IsAllowedInternalUrl(url));
    }

    [Fact]
    public void DataScheme_IsRejected()
    {
        var url = string.Concat("data", ":text/plain,unsafe");
        Assert.False(DentaLinkPolicy.IsAllowedInternalUrl(url));
    }

    [Fact]
    public void Filter_DropsUnsafeMalformedAndDuplicateLinks_AndCapsOutput()
    {
        var links = new[]
        {
            Link("External", "https://external.example/path"),
            Link("Doctors", "/pages/doctors.html"),
            Link("Doctors duplicate", "/pages/doctors.html"),
            new Dictionary<string, string> { ["text"] = "Missing url" },
            Link("Services", "/pages/services.html"),
            Link("Contact", "/pages/contact.html")
        };

        var result = DentaLinkPolicy.Filter(links);

        Assert.Equal(2, result.Count);
        Assert.Equal("/pages/doctors.html", result[0]["url"]);
        Assert.Equal("/pages/services.html", result[1]["url"]);
    }

    [Fact]
    public void Filter_RejectsOversizedLabelsAndUrls()
    {
        var links = new[]
        {
            Link(new string('x', 121), "/pages/contact.html"),
            Link("Long URL", "/pages/" + new string('a', 301))
        };

        Assert.Empty(DentaLinkPolicy.Filter(links));
    }

    private static Dictionary<string, string> Link(string text, string url)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = text,
            ["url"] = url
        };
}
