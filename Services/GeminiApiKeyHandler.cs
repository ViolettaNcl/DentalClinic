using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace DentalClinic.Services;

/// <summary>
/// Prepares outbound Gemini requests for the current API contract:
/// - sends the Gemini key via x-goog-api-key rather than the URL;
/// - removes the legacy ?key= query parameter;
/// - removes an accidental duplicate trailing user message produced when the
///   browser history already contains the current message and ChatController
///   appends req.Message again.
/// The handler only touches requests to generativelanguage.googleapis.com.
/// </summary>
public sealed class GeminiApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;

    public GeminiApiKeyHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri != null && uri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Remove("x-goog-api-key");
                request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

                var builder = new UriBuilder(uri);
                if (!string.IsNullOrEmpty(builder.Query))
                {
                    var filtered = builder.Query
                        .TrimStart('?')
                        .Split('&', StringSplitOptions.RemoveEmptyEntries)
                        .Where(pair => !pair.StartsWith("key=", StringComparison.OrdinalIgnoreCase));
                    builder.Query = string.Join("&", filtered);
                    request.RequestUri = builder.Uri;
                }
            }

            await RemoveDuplicateTrailingUserMessageAsync(request, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static async Task RemoveDuplicateTrailingUserMessageAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content == null ||
            !string.Equals(request.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return;

        var raw = await request.Content.ReadAsStringAsync(cancellationToken);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(raw);
        }
        catch
        {
            return;
        }

        if (root?["contents"] is not JsonArray contents || contents.Count < 2)
            return;

        var previous = contents[^2];
        var current = contents[^1];
        if (!IsSameUserMessage(previous, current))
            return;

        contents.RemoveAt(contents.Count - 1);
        request.Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
    }

    private static bool IsSameUserMessage(JsonNode? left, JsonNode? right)
    {
        if (!string.Equals(left?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(right?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase))
            return false;

        var leftText = left?["parts"]?[0]?["text"]?.GetValue<string>();
        var rightText = right?["parts"]?[0]?["text"]?.GetValue<string>();

        return !string.IsNullOrWhiteSpace(leftText) &&
               string.Equals(leftText, rightText, StringComparison.Ordinal);
    }
}
