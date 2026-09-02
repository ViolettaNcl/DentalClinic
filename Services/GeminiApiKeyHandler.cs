using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DentalClinic.Services;

/// <summary>
/// Central compatibility/security layer for Gemini calls.
/// - sends the key in x-goog-api-key and strips legacy ?key=;
/// - removes a duplicate trailing user message;
/// - for Denta chat requests, asks Gemini for schema-constrained JSON instead of
///   free-form marker text, then converts the structured result back to the
///   legacy controller contract so the existing UI keeps working unchanged.
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
        if (uri == null || !uri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            return await base.SendAsync(request, cancellationToken);

        ApplyApiKey(request);

        var isDentaChat = false;
        if (request.Content != null &&
            string.Equals(request.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await request.Content.ReadAsStringAsync(cancellationToken);
            JsonNode? root = null;
            try { root = JsonNode.Parse(raw); } catch { }

            if (root != null)
            {
                RemoveDuplicateTrailingUserMessage(root);
                isDentaChat = IsDentaChatRequest(root);
                if (isDentaChat)
                    EnforceStructuredChatResponse(root);

                request.Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
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
            var builder = new UriBuilder(request.RequestUri);
            builder.Path = builder.Path.Replace(":streamGenerateContent", ":generateContent", StringComparison.Ordinal);
            builder.Query = string.Join("&", builder.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.Equals("alt=sse", StringComparison.OrdinalIgnoreCase)));
            request.RequestUri = builder.Uri;
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (!isDentaChat || !response.IsSuccessStatusCode)
            return response;

        var responseRaw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!TryConvertStructuredCandidate(responseRaw, out var convertedJson))
            return response;

        if (synthesizeSse)
        {
            var sse = $"data: {convertedJson}\n\n";
            response.Content = new StringContent(sse, Encoding.UTF8, "text/event-stream");
        }
        else
        {
            response.Content = new StringContent(convertedJson, Encoding.UTF8, "application/json");
        }

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

    private static bool IsDentaChatRequest(JsonNode root)
    {
        var prompt = root["system_instruction"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        return prompt.Contains("Дента", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Dental Clinic", StringComparison.OrdinalIgnoreCase) && prompt.Contains("SUGGESTIONS", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnforceStructuredChatResponse(JsonNode root)
    {
        var promptNode = root["system_instruction"]?["parts"]?[0]?["text"];
        if (promptNode != null)
        {
            var prompt = promptNode.GetValue<string>();
            prompt += "\n\nSAFETY OVERRIDE: Never diagnose or infer a specific disease from symptoms. " +
                      "For severe facial/neck swelling, trouble breathing or swallowing, uncontrolled bleeding, major facial trauma, " +
                      "or fever with rapidly spreading swelling, tell the person to seek urgent emergency medical/dental care now. " +
                      "For other symptoms, recommend timely assessment by a dentist and use uncertainty language. " +
                      "Do not delay emergency care to continue this chat.\n" +
                      "OUTPUT OVERRIDE: Return only the JSON object required by the response schema. " +
                      "Set startBooking=true whenever the user expresses an intent to book, schedule, make an appointment, " +
                      "prendre rendez-vous, κλείσει ραντεβού, حجز موعد, or the equivalent in the current language.";
            root["system_instruction"]!["parts"]![0]!["text"] = prompt;
        }

        var generation = root["generationConfig"] as JsonObject ?? new JsonObject();
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

    private static bool TryConvertStructuredCandidate(string geminiJson, out string convertedJson)
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
                .Take(2)
                .Select(x => new Dictionary<string, string>
                {
                    ["text"] = x!["text"]?.GetValue<string>() ?? "",
                    ["url"] = x["url"]?.GetValue<string>() ?? ""
                })
                .Where(x => x["url"].StartsWith("/pages/", StringComparison.Ordinal))
                .ToList() ?? new List<Dictionary<string, string>>();

            var startBooking = structured["startBooking"]?.GetValue<bool>() == true;
            if (startBooking && !suggestions.Any(IsBookingSuggestion))
                suggestions.Add("Book an appointment");

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

    private static bool IsBookingSuggestion(string value)
    {
        var s = value.ToLowerInvariant();
        return s.Contains("запис") || s.Contains("приём") || s.Contains("appointment") || s.Contains("book") ||
               s.Contains("rendez") || s.Contains("ραντεβ") || s.Contains("حجز") || s.Contains("موعد");
    }
}
