using System.Reflection;
using System.Text.Json;
using DentalClinic.Models;
using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

/// <summary>
/// Deterministic provider-boundary matrix. No network/model wording is required for CI:
/// 5 languages x 8 conversation intents are fed through the same typed structured parser.
/// </summary>
public class DentaRegressionMatrixTests
{
    private static readonly (string Code, string BookingText)[] Languages =
    {
        ("ru", "Записаться на приём"),
        ("en", "Book an appointment"),
        ("fr", "Prendre rendez-vous"),
        ("el", "Κλείστε ραντεβού"),
        ("ar", "احجز موعدًا")
    };

    private static readonly (string Name, bool Booking)[] Scenarios =
    {
        ("price", false),
        ("doctor", false),
        ("booking", true),
        ("irrelevant", false),
        ("emergency", false),
        ("diagnosis", false),
        ("prompt-injection", false),
        ("unsupported-service", false)
    };

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var language in Languages)
        foreach (var scenario in Scenarios)
            yield return new object[] { language.Code, language.BookingText, scenario.Name, scenario.Booking };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void StructuredParser_IsSafeAndLocalizedAcrossFortyScenarios(
        string language,
        string localizedBooking,
        string scenario,
        bool expectsBooking)
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = $"SAFE-{language}-{scenario}",
            suggestions = Array.Empty<string>(),
            links = new object[]
            {
                new { text = "Clinic page", url = "/pages/doctors.html" },
                new { text = "External", url = "https://evil.example/steal" }
            },
            startBooking = expectsBooking
        });
        var provider = WrapGemini(structured);

        Assert.True(Parse(provider, language, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal($"SAFE-{language}-{scenario}", parsed!.Reply);
        Assert.Single(parsed.Links);
        Assert.Equal("/pages/doctors.html", parsed.Links[0].Url);
        Assert.DoesNotContain("evil.example", JsonSerializer.Serialize(parsed), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{\"reply\"", parsed.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUGGESTIONS:", parsed.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("LINKS:", parsed.Reply, StringComparison.Ordinal);

        if (expectsBooking)
            Assert.Contains(localizedBooking, parsed.Suggestions);
        else
            Assert.DoesNotContain(parsed.Suggestions, IsAnyBookingSuggestion);
    }

    [Fact]
    public void DentaSource_UsesCurrentModels_AdaptiveSafetyPrompt_AndCurrentStructuredFormat()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var ai = File.ReadAllText(Path.Combine(root, "Services/DentaAiService.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers/ChatController.cs"));
        var router = File.ReadAllText(Path.Combine(root, "Services/DentaClinicRouter.cs"));

        Assert.Contains("\"gemini-3.8-flash\"", ai, StringComparison.Ordinal);
        Assert.Contains("\"gemini-3.5-flash\"", ai, StringComparison.Ordinal);
        Assert.Contains("\"gemini-3.5-flash-lite\"", ai, StringComparison.Ordinal);
        Assert.Contains("responseFormat = new", ai, StringComparison.Ordinal);
        Assert.Contains("mimeType = \"application/json\"", ai, StringComparison.Ordinal);
        Assert.Contains("schema", ai, StringComparison.Ordinal);

        Assert.Contains("Do not diagnose a specific disease", ai, StringComparison.Ordinal);
        Assert.Contains("Do not prescribe medication", ai, StringComparison.Ordinal);
        Assert.Contains("difficulty breathing or swallowing", ai, StringComparison.Ordinal);
        Assert.Contains("Never invent a doctor, phone number, email, address, price, service", ai, StringComparison.Ordinal);
        Assert.Contains("Adapt length to the question", ai, StringComparison.Ordinal);

        Assert.Contains("_denta.AnswerAsync(", controller, StringComparison.Ordinal);
        Assert.Contains("DentaClinicRouter", router, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseModelOutput", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredParser_RejectsRawJsonReplyAndMalformedProviderPayload()
    {
        var rawReply = JsonSerializer.Serialize(new
        {
            reply = "{\"reply\":\"leaked\"}",
            suggestions = Array.Empty<string>(),
            links = Array.Empty<object>(),
            startBooking = false
        });

        Assert.False(Parse(WrapGemini(rawReply), "ru", out _));
        Assert.False(Parse("{not-json", "ru", out _));
    }

    private static bool Parse(string providerJson, string language, out DentaResponse? response)
    {
        var method = typeof(DentaAiService).GetMethod(
            "TryParseProviderResponse",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var args = new object?[] { providerJson, language, null };
        var ok = (bool)method!.Invoke(null, args)!;
        response = args[2] as DentaResponse;
        return ok;
    }

    private static bool IsAnyBookingSuggestion(string value) =>
        Languages.Any(language => string.Equals(language.BookingText, value, StringComparison.Ordinal));

    private static string WrapGemini(string structured) => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new { content = new { parts = new[] { new { text = structured } } } }
        }
    });
}
