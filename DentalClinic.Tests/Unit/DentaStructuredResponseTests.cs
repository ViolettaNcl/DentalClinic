using System.Reflection;
using System.Text.Json;
using DentalClinic.Services;

namespace DentalClinic.Tests.Unit;

public class DentaStructuredResponseTests
{
    [Theory]
    [InlineData("ru", "Записаться на приём")]
    [InlineData("en", "Book an appointment")]
    [InlineData("fr", "Prendre rendez-vous")]
    [InlineData("el", "Κλείστε ραντεβού")]
    [InlineData("ar", "احجز موعدًا")]
    public void BookingFallback_IsLocalized_AndSuggestionCountStaysBounded(string language, string expected)
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "ok",
            suggestions = new[] { "One", "Two", "Three" },
            links = Array.Empty<object>(),
            startBooking = true
        });

        var gemini = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = structured } } } }
            }
        });

        var converted = ConvertStructured(gemini, language);
        using var doc = JsonDocument.Parse(converted);
        var legacy = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString()!;

        Assert.Contains(expected, legacy, StringComparison.Ordinal);

        var marker = "SUGGESTIONS:";
        var linksMarker = "\nLINKS:";
        var start = legacy.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = legacy.IndexOf(linksMarker, StringComparison.Ordinal);
        var suggestionsJson = legacy[start..end];
        using var suggestionsDoc = JsonDocument.Parse(suggestionsJson);
        Assert.True(suggestionsDoc.RootElement.GetArrayLength() <= 3);
    }

    [Fact]
    public void StructuredLinks_DropExternalUrls()
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "ok",
            suggestions = Array.Empty<string>(),
            links = new object[]
            {
                new { text = "Internal", url = "/pages/services/implants.html" },
                new { text = "External", url = "https://example.com/phishing" }
            },
            startBooking = false
        });

        var gemini = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = structured } } } }
            }
        });

        var converted = ConvertStructured(gemini, "en");
        using var doc = JsonDocument.Parse(converted);
        var legacy = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString()!;

        Assert.Contains("/pages/services/implants.html", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", legacy, StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertStructured(string geminiJson, string language)
    {
        var method = typeof(GeminiApiKeyHandler).GetMethod(
            "TryConvertStructuredCandidate",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Denta structured converter not found");

        object?[] args = { geminiJson, language, null };
        var ok = (bool)(method.Invoke(null, args) ?? false);
        Assert.True(ok);
        return Assert.IsType<string>(args[2]);
    }
}
