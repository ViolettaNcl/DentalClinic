using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaStructuredResponseTests
{
    [Theory]
    [InlineData("ru", "Записаться на приём")]
    [InlineData("en", "Book an appointment")]
    [InlineData("fr", "Prendre rendez-vous")]
    [InlineData("el", "Κλείστε ραντεβού")]
    [InlineData("ar", "احجز موعدًا")]
    public void StructuredParser_AddsLocalizedBookingSuggestion_AndKeepsTypedReply(
        string language,
        string expectedBooking)
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "A safe reply",
            suggestions = new[] { "One", "Two", "Three" },
            links = Array.Empty<object>(),
            startBooking = true
        });

        var parsed = ParseProvider(WrapGemini(structured), language);

        Assert.Equal("A safe reply", parsed.Reply);
        Assert.Contains(expectedBooking, parsed.Suggestions);
        Assert.True(parsed.Suggestions.Count <= 3);
        Assert.DoesNotContain("SUGGESTIONS:", parsed.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("LINKS:", parsed.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredParser_KeepsOnlySafeInternalLinks()
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "ok",
            suggestions = Array.Empty<string>(),
            links = new object[]
            {
                new { text = "External", url = "https://example.com/phishing" },
                new { text = "Traversal", url = "/pages/../api/adminstats/summary" },
                new { text = "Internal", url = "/pages/services/implants.html" },
                new { text = "Backslash", url = "/pages/services\\implants.html" },
                new { text = "Blank", url = "" }
            },
            startBooking = false
        });

        var parsed = ParseProvider(WrapGemini(structured), "en");

        var link = Assert.Single(parsed.Links);
        Assert.Equal("/pages/services/implants.html", link.Url);
    }

    [Fact]
    public async Task ApiKeyHandler_StripsLegacyQueryKey_WithoutRewritingCurrentModel()
    {
        var capture = new CaptureHandler("{}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "secret-test-key"
            })
            .Build();
        using var handler = new GeminiApiKeyHandler(config) { InnerHandler = capture };
        using var client = new HttpClient(handler);

        var requestBody = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "Hello" } } }
            }
        });

        using var response = await client.PostAsync(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent?key=compat",
            new StringContent(requestBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capture.RequestUri);
        Assert.Equal("/v1beta/models/gemini-3.8-flash:generateContent", capture.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("key=", capture.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("secret-test-key", capture.ApiKeyHeader);
    }

    [Fact]
    public void DentaAiService_UsesCurrentModels_AndCurrentStructuredResponseFormat()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(root, "Services/DentaAiService.cs"));

        Assert.Contains("\"gemini-3.8-flash\"", source, StringComparison.Ordinal);
        Assert.Contains("\"gemini-3.5-flash\"", source, StringComparison.Ordinal);
        Assert.Contains("responseFormat", source, StringComparison.Ordinal);
        Assert.Contains("mimeType = \"application/json\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SUGGESTIONS:[", source, StringComparison.Ordinal);
    }

    private static DentaResponse ParseProvider(string providerJson, string language)
    {
        var method = typeof(DentaAiService).GetMethod(
            "TryParseProviderResponse",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Denta structured parser not found");

        object?[] args = { providerJson, language, null };
        var ok = (bool)(method.Invoke(null, args) ?? false);
        Assert.True(ok);
        return Assert.IsType<DentaResponse>(args[2]);
    }

    private static string WrapGemini(string structured) => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new { content = new { parts = new[] { new { text = structured } } } }
        }
    });

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public Uri? RequestUri { get; private set; }
        public string? ApiKeyHeader { get; private set; }

        public CaptureHandler(string responseBody) => _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.SingleOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
