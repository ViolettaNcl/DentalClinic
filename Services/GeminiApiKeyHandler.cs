using System.Net;

namespace DentalClinic.Services;

/// <summary>
/// Adds the Gemini API key as the current x-goog-api-key header and removes any
/// legacy ?key= query parameter before the request leaves the server. This keeps
/// authorization keys out of URLs/logs and is compatible with current Gemini REST auth.
/// </summary>
public sealed class GeminiApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;

    public GeminiApiKeyHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override Task<HttpResponseMessage> SendAsync(
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
                request.Headers.TryAddWithoutValidation("x-goog-api-client", "dental-clinic/1.0");

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
        }

        return base.SendAsync(request, cancellationToken);
    }
}
