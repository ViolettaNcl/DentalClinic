using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class GeminiApiKeyHandlerContentLifecycleTests
{
    [Fact]
    public async Task DentaTransformation_DisposesReplacedRequestAndResponseContent()
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            system_instruction = new
            {
                parts = new[] { new { text = "Ты Дента, ассистент Dental Clinic. SUGGESTIONS" } }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "hello" } } }
            },
            generationConfig = new { }
        });

        var structured = JsonSerializer.Serialize(new
        {
            reply = "Hello",
            suggestions = Array.Empty<string>(),
            links = Array.Empty<object>(),
            startBooking = false
        });
        var providerJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = structured } }
                    }
                }
            }
        });

        var requestContent = new TrackingContent(requestJson, "application/json");
        var upstream = new ProviderHandler(providerJson);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key"
            })
            .Build();

        using var handler = new GeminiApiKeyHandler(config) { InnerHandler = upstream };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=compat")
        {
            Content = requestContent
        };

        using var response = await client.SendAsync(request);
        var returned = await response.Content.ReadAsStringAsync();

        Assert.True(requestContent.IsDisposed);
        Assert.True(upstream.ResponseContent.IsDisposed);
        Assert.Contains("Hello", returned, StringComparison.Ordinal);
    }

    private sealed class ProviderHandler : HttpMessageHandler
    {
        public TrackingContent ResponseContent { get; }

        public ProviderHandler(string responseJson)
        {
            ResponseContent = new TrackingContent(responseJson, "application/json");
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = ResponseContent
            });
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        private readonly byte[] _bytes;

        public bool IsDisposed { get; private set; }

        public TrackingContent(string value, string mediaType)
        {
            _bytes = Encoding.UTF8.GetBytes(value);
            Headers.ContentType = new MediaTypeHeaderValue(mediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_bytes, 0, _bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
