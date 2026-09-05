using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;

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

        var gemini = WrapGemini(structured);
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

        var converted = ConvertStructured(WrapGemini(structured), "en");
        using var doc = JsonDocument.Parse(converted);
        var legacy = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString()!;

        Assert.Contains("/pages/services/implants.html", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", legacy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DentaRequest_UsesCurrentFlashModel_StripsLegacySampling_AndHidesApiKey()
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "Hello",
            suggestions = Array.Empty<string>(),
            links = Array.Empty<object>(),
            startBooking = false
        });
        var capture = new CaptureHandler(WrapGemini(structured));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = "secret-test-key" })
            .Build();
        var handler = new GeminiApiKeyHandler(config) { InnerHandler = capture };
        using var client = new HttpClient(handler);

        var requestBody = JsonSerializer.Serialize(new
        {
            system_instruction = new
            {
                parts = new[] { new { text = "Ты — Дента, AI Dental Clinic. SUGGESTIONS. ЯЗЫК: отвечай ТОЛЬКО на английском (English)." } }
            },
            contents = new[] { new { role = "user", parts = new[] { new { text = "Book me" } } } },
            generationConfig = new { temperature = 0.2, topP = 0.8, topK = 20, maxOutputTokens = 300 }
        });

        using var response = await client.PostAsync(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse&key=compat",
            new StringContent(requestBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capture.RequestUri);
        Assert.Contains("/models/gemini-3.8-flash:generateContent", capture.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("key=", capture.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("secret-test-key", capture.ApiKeyHeader);

        using var forwarded = JsonDocument.Parse(capture.RequestBody!);
        var generation = forwarded.RootElement.GetProperty("generationConfig");
        Assert.False(generation.TryGetProperty("temperature", out _));
        Assert.False(generation.TryGetProperty("topP", out _));
        Assert.False(generation.TryGetProperty("topK", out _));
        Assert.True(generation.TryGetProperty("responseSchema", out _));
        Assert.Equal("application/json", generation.GetProperty("responseMimeType").GetString());
    }

    private static string WrapGemini(string structured) => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new { content = new { parts = new[] { new { text = structured } } } }
        }
    });

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

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? ApiKeyHeader { get; private set; }

        public CaptureHandler(string responseBody) => _responseBody = responseBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            ApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.SingleOrDefault() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
