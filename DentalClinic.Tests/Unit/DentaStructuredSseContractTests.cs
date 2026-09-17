using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaStructuredSseContractTests
{
    [Fact]
    public void BrowserSse_UsesTypedValidatedResponse_WithoutLegacyProviderStreamingParser()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers/ChatController.cs"));
        var ai = File.ReadAllText(Path.Combine(root, "Services/DentaAiService.cs"));
        var handler = File.ReadAllText(Path.Combine(root, "Services/GeminiApiKeyHandler.cs"));

        // Public transport remains SSE, but the controller emits the already validated
        // typed Denta reply and a separate final metadata event.
        Assert.Contains("Response.ContentType = \"text/event-stream\"", controller, StringComparison.Ordinal);
        Assert.Contains("await SendAsync(new { delta = response.Reply });", controller, StringComparison.Ordinal);
        Assert.Contains("done = true", controller, StringComparison.Ordinal);
        Assert.Contains("response.Suggestions", controller, StringComparison.Ordinal);
        Assert.Contains("response.Links", controller, StringComparison.Ordinal);

        // Provider output is schema constrained and parsed exactly once by DentaAiService.
        Assert.Contains("responseFormat = new", ai, StringComparison.Ordinal);
        Assert.Contains("mimeType = \"application/json\"", ai, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<DentaResponse>", ai, StringComparison.Ordinal);

        // The handler is now only the secret/cancellation boundary. It must not turn
        // Gemini JSON into marker text or synthesize provider SSE events.
        Assert.DoesNotContain("streamGenerateContent", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TryConvertStructuredCandidate", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("SUGGESTIONS:", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("LINKS:", handler, StringComparison.Ordinal);

        Assert.DoesNotContain("ParseModelOutput", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("SUGGESTIONS:", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("LINKS:", controller, StringComparison.Ordinal);
    }
}
