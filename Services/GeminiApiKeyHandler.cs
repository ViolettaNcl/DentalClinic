using System.Text;
using System.Text.Json.Nodes;

namespace DentalClinic.Services;

/// <summary>
/// Security/transport boundary shared by Gemini callers.
/// - injects Gemini:ApiKey through x-goog-api-key;
/// - strips any legacy key= query parameter so secrets never enter URLs/logs;
/// - removes an accidental duplicated trailing user turn;
/// - links provider work to the current ASP.NET request lifetime.
///
/// Denta response shaping intentionally does NOT happen here. DentaAiService owns
/// the typed response schema and parsing so provider JSON is deserialized exactly
/// once and can never leak into the patient UI through a legacy text conversion.
/// </summary>
public sealed class GeminiApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GeminiApiKeyHandler(
        IConfiguration configuration,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor ?? new HttpContextAccessor();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri == null || !uri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            return await base.SendAsync(request, cancellationToken);

        var requestAborted = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            requestAborted);
        var effectiveCancellation = linkedCancellation.Token;

        ApplyApiKey(request);
        await RemoveDuplicateTrailingUserMessageAsync(request, effectiveCancellation);

        return await base.SendAsync(request, effectiveCancellation);
    }

    private void ApplyApiKey(HttpRequestMessage request)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Remove("x-goog-api-key");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        }

        var builder = new UriBuilder(request.RequestUri!);
        if (!string.IsNullOrEmpty(builder.Query))
        {
            var filtered = builder.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(pair => !pair.StartsWith("key=", StringComparison.OrdinalIgnoreCase));
            builder.Query = string.Join("&", filtered);
            request.RequestUri = builder.Uri;
        }
    }

    private static async Task RemoveDuplicateTrailingUserMessageAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content == null
            || !string.Equals(
                request.Content.Headers.ContentType?.MediaType,
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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

        if (!IsSameUserMessage(contents[^2], contents[^1]))
            return;

        contents.RemoveAt(contents.Count - 1);

        var originalContent = request.Content;
        request.Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
        originalContent.Dispose();
    }

    private static bool IsSameUserMessage(JsonNode? left, JsonNode? right)
    {
        if (!string.Equals(left?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(right?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leftText = left?["parts"]?[0]?["text"]?.GetValue<string>();
        var rightText = right?["parts"]?[0]?["text"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(leftText)
               && string.Equals(leftText, rightText, StringComparison.Ordinal);
    }
}
