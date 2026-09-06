using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DentalClinic.Services;

/// <summary>
/// Central compatibility/security layer for Gemini calls.
/// - sends the key in x-goog-api-key and strips legacy ?key=;
/// - removes a duplicate trailing user message;
/// - upgrades Denta's legacy model aliases to current stable Flash models;
/// - links provider work to the current ASP.NET request lifetime so abandoned
///   browser requests stop consuming upstream resources;
/// - for Denta chat requests, asks Gemini for schema-constrained JSON instead of
///   free-form marker text, then converts the structured result back to the
///   legacy controller contract so the existing UI keeps working unchanged.
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
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, requestAborted);
        var effectiveCancellation = linkedCancellation.Token;

        ApplyApiKey(request);

        var isDentaChat = false;
        var dentaLanguage = "ru";
        if (request.Content != null &&
            string.Equals(request.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await request.Content.ReadAsStringAsync(effectiveCancellation);
            JsonNode? root = null;
            try { root = JsonNode.Parse(raw); } catch { }

            if (root != null)
            {
                RemoveDuplicateTrailingUserMessage(root);
                isDentaChat = IsDentaChatRequest(root);
                if (isDentaChat)
                {
                    dentaLanguage = DetectDentaLanguage(root);
                    EnforceStructuredChatResponse(root);
                    UpgradeDentaModel(request);
                }

                var originalContent = request.Content;
                request.Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
                originalContent.Dispose();
            }
        }

        // Gemini streaming of a schema-constrained JSON object arrives as partial
        // JSON fragments. The legacy ChatController expects textual reply chunks.
        // For Denta only, request one structured response and expose it back as a
        // synthetic SSE event. The browser-facing /api/chat/stream contract remains
        // intact while the model output itself is now strictly structured.
        var synthesizeSse = isDentaChat && request.RequestUri!.AbsolutePath.Contains(":streamGenerateContent", StringComparison.Ordinal);
        if (synthesizeSse)
        {
            var builder = new UriBuilder(request.RequestUri!);
            builder.Path = builder.Path.Replace(":streamGenerateContent", ":generateContent", StringComparison.Ordinal);
            builder.Query = string.Join("&", builder.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.Equals("alt=sse", StringComparison.OrdinalIgnoreCase)));
            request.RequestUri = builder.Uri;
        }

        var response = await base.SendAsync(request, effectiveCancellation);
        if (!isDentaChat || !response.IsSuccessStatusCode)
            return response;

        var responseRaw = await response.Content.ReadAsStringAsync(effectiveCancellation);
        if (!TryConvertStructuredCandidate(responseRaw, dentaLanguage, out var convertedJson))
            return response;

        var originalResponseContent = response.Content;
        if (synthesizeSse)
        {
            var sse = $"data: {convertedJson}\n\n";
            response.Content = new StringContent(sse, Encoding.UTF8, "text/event-stream");
        }
        else
        {
            response.Content = new StringContent(convertedJson, Encoding.UTF8, "application/json");
        }
        originalResponseContent.Dispose();

        return response;
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

    private static void UpgradeDentaModel(HttpRequestMessage request)
    {
        if (request.RequestUri == null) return;
        var builder = new UriBuilder(request.RequestUri);
        var path = builder.Path
            .Replace("/models/gemini-2.5-flash-lite:", "/models/gemini-3.5-flash-lite:", StringComparison.Ordinal)
            .Replace("/models/gemini-2.5-flash:", "/models/gemini-3.8-flash:", StringComparison.Ordinal);
        if (!string.Equals(path, builder.Path, StringComparison.Ordinal))
        {
            builder.Path = path;
            request.RequestUri = builder.Uri;
        }
    }

    private static bool IsDentaChatRequest(JsonNode root)
    {
        var prompt = root["system_instruction"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        return prompt.Contains("Дента", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Dental Clinic", StringComparison.OrdinalIgnoreCase) && prompt.Contains("SUGGESTIONS", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectDentaLanguage(JsonNode root)
    {
        var prompt = root["system_instruction"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        if (prompt.Contains("английском", StringComparison.OrdinalIgnoreCase) || prompt.Contains("English", StringComparison.OrdinalIgnoreCase)) return "en";
        if (prompt.Contains("французском", StringComparison.OrdinalIgnoreCase) || prompt.Contains("français", StringComparison.OrdinalIgnoreCase)) return "fr";
        if (prompt.Contains("греческом", StringComparison.OrdinalIgnoreCase) || prompt.Contains("ελληνικά", StringComparison.OrdinalIgnoreCase)) return "el";
        if (prompt.Contains("арабском", StringComparison.OrdinalIgnoreCase) || prompt.Contains("العربية", StringComparison.OrdinalIgnoreCase)) return "ar";
        return "ru";
    }

    private static void EnforceStructuredChatResponse(JsonNode root)
    {
        var promptNode = root["system_instruction"]?["parts"]?[0]?["text"];
        if (promptNode != null)
        {
            var prompt = promptNode.GetValue<string>();
            prompt += "\n\nSAFETY OVERRIDE: Never diagnose or infer a specific disease from symptoms, and never recommend an invasive procedure solely from symptoms. " +
                      "Do not prescribe medicines, antibiotics, painkillers, dosages, or medication schedules. " +
                      "Do not promise that treatment will be painless, guarantee an outcome, or present an estimated price as a final treatment cost before examination. " +
                      "For severe facial/neck swelling, trouble breathing or swallowing, uncontrolled bleeding, major facial trauma, " +
                      "or fever with rapidly spreading swelling, tell the person to seek urgent emergency medical/dental care now. " +
                      "For other symptoms, use uncertainty language, explain that several causes are possible, and recommend timely assessment by a dentist. " +
                      "Do not delay emergency care to continue this chat.\n" +
                      "OUTPUT OVERRIDE: Return only the JSON object required by the response schema. " +
                      "Set startBooking=true whenever the user expresses an intent to book, schedule, make an appointment, " +
                      "prendre rendez-vous, κλείσει ραντεβού, حجز موعد, or the equivalent in the current language. " +
                      "Keep reply, suggestions, and link text in the language explicitly requested by the system prompt.";
            root["system_instruction"]!["parts"]![0]!["text"] = prompt;
        }

        var generation = root["generationConfig"] as JsonObject ?? new JsonObject();

        // Gemini 3.x is optimized for default sampling. Remove older tuning fields
        // while keeping deterministic behavior through the strict response schema
        // and explicit system rules.
        generation.Remove("temperature");
        generation.Remove("topP");
        generation.Remove("topK");
        generation.Remove("top_p");
        generation.Remove("top_k");
        generation.Remove("candidateCount");
        generation.Remove("candidate_count");

        generation["responseMimeType"] = "application/json";
        generation["responseSchema"] = JsonNode.Parse("""
        {
          "type":"OBJECT",
          "properties":{
            "reply":{"type":"STRING"},
            "suggestions":{"type":"ARRAY","items":{"type":"STRING"}},
            "links":{"type":"ARRAY","items":{"type":"OBJECT","properties":{"text":{"type":"STRING"},"url":{"type":"STRING"}},"required":["text","url"]}},
            "startBooking":{"type":"BOOLEAN"}
          },
          "required":["reply","suggestions","links","startBooking"]
        }
        """);
        root["generationConfig"] = generation;
    }

    private static void RemoveDuplicateTrailingUserMessage(JsonNode root)
    {
        if (root["contents"] is not JsonArray contents || contents.Count < 2)
            return;

        if (IsSameUserMessage(contents[^2], contents[^1]))
            contents.RemoveAt(contents.Count - 1);
    }

    private static bool IsSameUserMessage(JsonNode? left, JsonNode? right)
    {
        if (!string.Equals(left?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(right?["role"]?.GetValue<string>(), "user", StringComparison.OrdinalIgnoreCase))
            return false;

        var leftText = left?["parts"]?[0]?["text"]?.GetValue<string>();
        var rightText = right?["parts"]?[0]?["text"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(leftText) && string.Equals(leftText, rightText, StringComparison.Ordinal);
    }

    private static bool TryConvertStructuredCandidate(string geminiJson, string language, out string convertedJson)
    {
        convertedJson = geminiJson;
        try
        {
            var root = JsonNode.Parse(geminiJson);
            var textNode = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];
            if (root == null || textNode == null) return false;

            var structured = JsonNode.Parse(textNode.GetValue<string>()) as JsonObject;
            if (structured == null) return false;

            var reply = structured["reply"]?.GetValue<string>()?.Trim() ?? "";
            var suggestions = structured["suggestions"]?.AsArray()
                .Select(x => x?.GetValue<string>() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(3)
                .ToList() ?? new List<string>();
            var links = structured["links"]?.AsArray()
                .Select(x => x as JsonObject)
                .Where(x => x != null)
                .Select(x => new Dictionary<string, string>
                {
                    ["text"] = x!["text"]?.GetValue<string>()?.Trim() ?? "",
                    ["url"] = x["url"]?.GetValue<string>()?.Trim() ?? ""
                })
                .Where(x => !string.IsNullOrWhiteSpace(x["text"]) && IsSafeLocalPageLink(x["url"]))
                .Take(2)
                .ToList() ?? new List<Dictionary<string, string>>();

            var startBooking = structured["startBooking"]?.GetValue<bool>() == true;
            if (startBooking && !suggestions.Any(IsBookingSuggestion))
            {
                var localizedBooking = BookingSuggestionFor(language);
                if (suggestions.Count >= 3) suggestions[^1] = localizedBooking;
                else suggestions.Add(localizedBooking);
            }

            var legacy = reply + "\nSUGGESTIONS:" + JsonSerializer.Serialize(suggestions) +
                         "\nLINKS:" + JsonSerializer.Serialize(links);
            root["candidates"]![0]!["content"]!["parts"]![0]!["text"] = legacy;
            convertedJson = root.ToJsonString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSafeLocalPageLink(string url) =>
        url.StartsWith("/pages/", StringComparison.Ordinal)
        && ServiceCatalogPolicy.IsValidPageUrl(url);

    private static string BookingSuggestionFor(string language) => language switch
    {
        "en" => "Book an appointment",
        "fr" => "Prendre rendez-vous",
        "el" => "Κλείστε ραντεβού",
        "ar" => "احجز موعدًا",
        _ => "Записаться на приём"
    };

    private static bool IsBookingSuggestion(string value)
    {
        var s = value.ToLowerInvariant();
        return s.Contains("запис") || s.Contains("приём") || s.Contains("appointment") || s.Contains("book") ||
               s.Contains("rendez") || s.Contains("ραντεβ") || s.Contains("κλείσ") || s.Contains("حجز") || s.Contains("موعد");
    }
}
