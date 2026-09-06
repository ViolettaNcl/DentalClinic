using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaStructuredSseContractTests
{
    [Fact]
    public async Task StructuredDentaStream_UsesValidatedSingleProviderEvent_AndKeepsBrowserSseContract()
    {
        var structured = JsonSerializer.Serialize(new
        {
            reply = "A short safe answer.",
            suggestions = new[] { "Tell me more" },
            links = new[] { new { text = "Implants", url = "/pages/services/implants.html" } },
            startBooking = true
        });

        var providerResponse = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = structured } } } }
            }
        });

        var capture = new CaptureHandler(providerResponse);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "stream-contract-test-key"
            })
            .Build();

        using var handler = new GeminiApiKeyHandler(config) { InnerHandler = capture };
        using var client = new HttpClient(handler);

        var requestBody = JsonSerializer.Serialize(new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = "Ты — Дента, AI Dental Clinic. SUGGESTIONS. ЯЗЫК: отвечай ТОЛЬКО на английском (English)."
                    }
                }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "I want to book an implant consultation" } } }
            },
            generationConfig = new { maxOutputTokens = 300 }
        });

        using var response = await client.PostAsync(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse&key=compat",
            new StringContent(requestBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capture.RequestUri);
        Assert.Contains(":generateContent", capture.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain(":streamGenerateContent", capture.RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("alt=sse", capture.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("key=", capture.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("stream-contract-test-key", capture.ApiKeyHeader);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var sse = await response.Content.ReadAsStringAsync();
        var events = sse.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Deliberate reliability-first contract: schema-constrained Gemini output is
        // validated as one complete object before it is exposed as synthetic SSE.
        Assert.Single(events);
        Assert.StartsWith("data: ", events[0], StringComparison.Ordinal);

        using var eventDoc = JsonDocument.Parse(events[0][6..]);
        var legacyText = eventDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        Assert.NotNull(legacyText);
        Assert.StartsWith("A short safe answer.", legacyText, StringComparison.Ordinal);
        Assert.Contains("SUGGESTIONS:", legacyText, StringComparison.Ordinal);
        Assert.Contains("Book an appointment", legacyText, StringComparison.Ordinal);
        Assert.Contains("LINKS:", legacyText, StringComparison.Ordinal);
        Assert.Contains("/pages/services/implants.html", legacyText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reply\"", legacyText, StringComparison.Ordinal);
    }

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
