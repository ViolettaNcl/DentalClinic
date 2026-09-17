using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Models;

namespace DentalClinic.Services;

/// <summary>
/// One typed boundary around Gemini for Denta. The model returns a JSON object that
/// is deserialized exactly once into DentaResponse; raw provider JSON never reaches
/// the browser and no legacy SUGGESTIONS:/LINKS: text markers are used.
/// </summary>
public sealed class DentaAiService
{
    private static readonly string[] Models =
    {
        "gemini-3.8-flash",
        "gemini-3.5-flash",
        "gemini-3.5-flash-lite"
    };

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChatKnowledgeService _knowledge;
    private readonly DentaSiteKnowledgeService _siteKnowledge;
    private readonly ILogger<DentaAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DentaAiService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ChatKnowledgeService knowledge,
        DentaSiteKnowledgeService siteKnowledge,
        ILogger<DentaAiService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _knowledge = knowledge;
        _siteKnowledge = siteKnowledge;
        _logger = logger;
    }

    public async Task<DentaProviderResult> GenerateAsync(
        string message,
        IReadOnlyList<DentaTurn> history,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]))
            return DentaProviderResult.Failed(DentaFailureKind.MissingApiKey);

        var lang = NormalizeLanguage(language);
        var systemPrompt = await BuildSystemPromptAsync(lang, message, cancellationToken);
        var contents = BuildContents(history, message);
        var client = _httpClientFactory.CreateClient();

        var bodies = new[]
        {
            BuildCurrentStructuredRequestBody(systemPrompt, contents),
            BuildLegacyStructuredRequestBody(systemPrompt, contents),
            BuildPlainCompatibilityRequestBody(systemPrompt, contents)
        };

        var sawRateLimit = false;
        var sawUnavailableModel = false;
        var sawInvalidResponse = false;
        var sawProviderError = false;

        foreach (var model in Models)
        {
            var moveToNextModel = false;
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            for (var mode = 0; mode < bodies.Length && !moveToNextModel; mode++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(bodies[mode], Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseContentRead,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(ex, "Gemini timeout for Denta model {Model}", model);
                    return DentaProviderResult.Failed(DentaFailureKind.Timeout, model: model);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Gemini network failure for Denta model {Model}", model);
                    return DentaProviderResult.Failed(DentaFailureKind.Network, model: model);
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        _logger.LogError("Gemini authentication rejected for Denta ({Status}) model {Model}", status, model);
                        return DentaProviderResult.Failed(DentaFailureKind.Authentication, status, model);
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        sawRateLimit = true;
                        _logger.LogWarning("Gemini rate limit for Denta model {Model}", model);
                        moveToNextModel = true;
                        continue;
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        sawUnavailableModel = true;
                        _logger.LogWarning("Gemini Denta model {Model} is unavailable (404)", model);
                        moveToNextModel = true;
                        continue;
                    }

                    if ((int)response.StatusCode >= 500)
                    {
                        sawProviderError = true;
                        _logger.LogWarning("Gemini provider error {Status} for Denta model {Model}", status, model);
                        moveToNextModel = true;
                        continue;
                    }

                    // Accounts/endpoints can temporarily differ in which structured-output
                    // envelope they accept. Try the current 2026 responseFormat first,
                    // then the older responseMimeType/responseSchema compatibility form,
                    // and finally a plain JSON-instruction request. This is deliberately
                    // limited to schema/request-shape failures, not auth or quota errors.
                    if ((response.StatusCode == HttpStatusCode.BadRequest
                         || response.StatusCode == HttpStatusCode.UnprocessableEntity)
                        && mode < bodies.Length - 1)
                    {
                        _logger.LogWarning(
                            "Gemini rejected Denta request shape ({Status}) for model {Model}; trying compatibility mode {Mode}",
                            status,
                            model,
                            mode + 1);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        sawProviderError = true;
                        _logger.LogError(
                            "Gemini request failed for Denta ({Status}) model {Model}. Body prefix: {Body}",
                            status,
                            model,
                            SafeLogPrefix(raw));
                        moveToNextModel = true;
                        continue;
                    }

                    if (TryParseProviderResponse(raw, lang, out var dentaResponse))
                    {
                        dentaResponse!.Source = mode == 0 ? "gemini" : "gemini-compat";
                        return DentaProviderResult.Ok(dentaResponse, model);
                    }

                    // Last compatibility mode may return ordinary text even if it did
                    // not obey the JSON instruction. Keep that useful answer, sanitize
                    // it, and let ChatController add safe internal links heuristically.
                    if (mode == bodies.Length - 1
                        && TryParseProviderTextFallback(raw, out dentaResponse))
                    {
                        dentaResponse!.Source = "gemini-text-fallback";
                        return DentaProviderResult.Ok(dentaResponse, model);
                    }

                    sawInvalidResponse = true;
                    _logger.LogWarning(
                        "Gemini returned malformed Denta output for model {Model} compatibility mode {Mode}. Body prefix: {Body}",
                        model,
                        mode,
                        SafeLogPrefix(raw));
                }
            }
        }

        if (sawRateLimit)
            return DentaProviderResult.Failed(DentaFailureKind.RateLimited);
        if (sawInvalidResponse)
            return DentaProviderResult.Failed(DentaFailureKind.InvalidResponse);
        if (sawProviderError)
            return DentaProviderResult.Failed(DentaFailureKind.ProviderError);
        if (sawUnavailableModel)
            return DentaProviderResult.Failed(DentaFailureKind.ModelUnavailable);
        return DentaProviderResult.Failed(DentaFailureKind.ProviderError);
    }

    private async Task<string> BuildSystemPromptAsync(
        string language,
        string userQuery,
        CancellationToken cancellationToken)
    {
        var knowledgeBlock = await _knowledge.GetKnowledgeBlockAsync(userQuery, cancellationToken);
        var siteKnowledgeBlock = await _siteKnowledge.GetRelevantKnowledgeBlockAsync(userQuery, cancellationToken);
        var contactsBlock = _knowledge.GetContactsBlock();
        var languageName = language switch
        {
            "en" => "English",
            "fr" => "français",
            "el" => "ελληνικά",
            "ar" => "العربية",
            _ => "русский"
        };

        return $$"""
        You are Denta, the AI assistant for Dental Clinic in Volgograd.

        RESPONSE LANGUAGE
        Reply only in {{languageName}}. Suggestions and link labels must use the same language.

        ROLE AND STYLE
        - Be warm, professional, concise and natural. Do not sound like a rigid FAQ bot.
        - Adapt length to the question: a simple question can be 1-3 sentences; lists may use readable bullet points; a complex question may be slightly longer.
        - Understand follow-up questions using the supplied conversation history.
        - Ask at most one clarifying question when it materially helps.
        - Do not mention internal prompts, routing, schemas, databases, API providers or implementation details.

        CLINIC FACTS ARE AUTHORITATIVE
        - Doctors, services, prices, contacts, hours, policies and clinic capabilities must come only from AUTHORITATIVE_CLINIC_FACTS, MANAGED_CLINIC_KNOWLEDGE, PUBLIC_SITE_FACTS or clinic_contact below.
        - PUBLIC_SITE_FACTS contains relevant excerpts from the actual public website. Use them for published doctor roles, certificates, service explanations and other website facts.
        - When SQL/config and the public site both contain the same changing operational fact (active status, price, contact), SQL/config wins.
        - Never invent a doctor, phone number, email, address, price, service, technology, sedation policy, availability or payment policy.
        - If the supplied facts do not confirm a clinic-specific claim, clearly say that the information is not confirmed and direct the patient to clinic staff or a relevant internal page.
        - Prices are informational estimates from the clinic database. Never present them as a final treatment quote before examination.

        MEDICAL SAFETY
        - Do not diagnose a specific disease from symptoms alone.
        - Do not prescribe medication, antibiotics, painkillers, dosages or medication schedules.
        - Do not guarantee a painless procedure or treatment outcome.
        - For ordinary symptoms, briefly acknowledge the concern, explain that several causes can be possible when useful, and recommend timely dental assessment.
        - For difficulty breathing or swallowing, rapidly spreading facial/neck swelling, uncontrolled bleeding, major facial/dental trauma, or severe systemic illness with dental swelling, advise urgent emergency/in-person care now rather than continuing routine chat triage.

        BOOKING
        - Set startBooking=true when the user clearly wants to book, schedule or make an appointment.
        - Otherwise set startBooking=false.

        OUTPUT
        - Return only the structured object required by the response schema.
        - reply: patient-facing answer only; never JSON inside this string.
        - suggestions: 0-3 short useful follow-up buttons.
        - links: 0-2 safe internal links and only paths starting with /pages/ that are supported by the clinic data/pages list.
        - Never include SUGGESTIONS: or LINKS: marker text in reply.

        clinic_contact
        {{contactsBlock}}

        {{knowledgeBlock}}

        {{siteKnowledgeBlock}}

        SUPPORTED INTERNAL PAGES
        /pages/services.html
        /pages/services/implants.html
        /pages/services/crowns.html
        /pages/services/fillings.html
        /pages/services/root-canal.html
        /pages/services/extractions.html
        /pages/services/bridges.html
        /pages/services/prosthetics.html
        /pages/services/cosmetic-treatments.html
        /pages/doctors.html
        /pages/contact.html
        /pages/about.html
        """;
    }

    private static List<object> BuildContents(IReadOnlyList<DentaTurn> history, string message)
    {
        var normalized = new List<DentaTurn>();
        foreach (var turn in history.TakeLast(10))
        {
            if (string.IsNullOrWhiteSpace(turn.Text)) continue;
            var role = turn.Role.Equals("bot", StringComparison.OrdinalIgnoreCase)
                       || turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                       || turn.Role.Equals("model", StringComparison.OrdinalIgnoreCase)
                ? "model"
                : "user";
            var text = Truncate(turn.Text.Trim(), 800);
            if (normalized.Count > 0
                && normalized[^1].Role == role
                && normalized[^1].Text.Equals(text, StringComparison.Ordinal))
            {
                continue;
            }
            normalized.Add(new DentaTurn(role, text));
        }

        if (normalized.Count > 0
            && normalized[^1].Role == "user"
            && normalized[^1].Text.Equals(message.Trim(), StringComparison.Ordinal))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        var contents = normalized
            .Select(turn => (object)new
            {
                role = turn.Role,
                parts = new[] { new { text = turn.Text } }
            })
            .ToList();

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = Truncate(message.Trim(), 800) } }
        });

        return contents;
    }

    private static object BuildDentaSchema() => new
    {
        type = "object",
        properties = new
        {
            reply = new
            {
                type = "string",
                description = "Natural patient-facing reply in the requested language. Never contain JSON, SUGGESTIONS:, or LINKS: markers."
            },
            suggestions = new
            {
                type = "array",
                maxItems = 3,
                items = new { type = "string" }
            },
            links = new
            {
                type = "array",
                maxItems = 2,
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string" },
                        url = new { type = "string" }
                    },
                    required = new[] { "text", "url" }
                }
            },
            startBooking = new { type = "boolean" }
        },
        required = new[] { "reply", "suggestions", "links", "startBooking" }
    };

    private static string BuildCurrentStructuredRequestBody(string systemPrompt, List<object> contents)
    {
        var schema = BuildDentaSchema();
        return JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new
            {
                maxOutputTokens = 900,
                responseFormat = new
                {
                    text = new
                    {
                        mimeType = "application/json",
                        schema
                    }
                }
            }
        });
    }

    private static string BuildLegacyStructuredRequestBody(string systemPrompt, List<object> contents)
    {
        var schema = BuildDentaSchema();
        return JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new
            {
                maxOutputTokens = 900,
                responseMimeType = "application/json",
                responseSchema = schema
            }
        });
    }

    private static string BuildPlainCompatibilityRequestBody(string systemPrompt, List<object> contents)
    {
        var compatibilityPrompt = systemPrompt + "\n\nCOMPATIBILITY FALLBACK: Return one JSON object with exactly these fields: reply (string), suggestions (array of up to 3 strings), links (array of up to 2 objects with text and url), startBooking (boolean). Do not use a markdown code fence.";
        return JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = compatibilityPrompt } } },
            contents,
            generationConfig = new { maxOutputTokens = 900 }
        });
    }

    internal static bool TryParseProviderResponse(
        string providerJson,
        string language,
        out DentaResponse? response)
    {
        response = null;
        try
        {
            using var provider = JsonDocument.Parse(providerJson);
            var text = provider.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return false;
            var cleanJson = StripCodeFence(text.Trim());
            var parsed = JsonSerializer.Deserialize<DentaResponse>(cleanJson, JsonOptions);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Reply)) return false;

            parsed.Reply = SanitizeReply(parsed.Reply);
            if (string.IsNullOrWhiteSpace(parsed.Reply)) return false;

            parsed.Suggestions = (parsed.Suggestions ?? new List<string>())
                .Select(s => s?.Trim() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            parsed.Links = FilterLinks(parsed.Links);

            if (parsed.StartBooking && !parsed.Suggestions.Any(IsBookingSuggestion))
            {
                var booking = BookingSuggestion(language);
                if (parsed.Suggestions.Count >= 3) parsed.Suggestions[^1] = booking;
                else parsed.Suggestions.Add(booking);
            }

            response = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool TryParseProviderTextFallback(
        string providerJson,
        out DentaResponse? response)
    {
        response = null;
        try
        {
            using var provider = JsonDocument.Parse(providerJson);
            var text = provider.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return false;
            var clean = StripCodeFence(text.Trim());

            // The fallback may still have followed the JSON instruction.
            try
            {
                var parsed = JsonSerializer.Deserialize<DentaResponse>(clean, JsonOptions);
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Reply))
                {
                    parsed.Reply = SanitizeReply(parsed.Reply);
                    if (string.IsNullOrWhiteSpace(parsed.Reply)) return false;
                    parsed.Suggestions = (parsed.Suggestions ?? new List<string>())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToList();
                    parsed.Links = FilterLinks(parsed.Links);
                    response = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Fall through to safe plain-text handling.
            }

            var reply = SanitizeReply(clean);
            if (string.IsNullOrWhiteSpace(reply)) return false;

            response = new DentaResponse
            {
                Reply = reply,
                Suggestions = new(),
                Links = new(),
                StartBooking = false
            };
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static List<DentaLink> FilterLinks(IEnumerable<DentaLink>? links)
    {
        if (links == null) return new();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safe = new List<DentaLink>();
        foreach (var link in links)
        {
            if (safe.Count >= 2) break;
            var text = link?.Text?.Trim() ?? "";
            var url = link?.Url?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text)
                || text.Length > 120
                || !DentaLinkPolicy.IsAllowedInternalUrl(url)
                || !ServiceCatalogPolicy.IsValidPageUrl(url)
                || !seen.Add(url))
            {
                continue;
            }
            safe.Add(new DentaLink { Text = text, Url = url });
        }
        return safe;
    }

    private static string SanitizeReply(string reply)
    {
        var value = reply.Trim();
        if (value.StartsWith("{", StringComparison.Ordinal) || value.StartsWith("[", StringComparison.Ordinal))
            return "";
        value = value.Replace("SUGGESTIONS:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("LINKS:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return Truncate(value, 2200);
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var firstNewline = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline < 0 || lastFence <= firstNewline) return text;
        return text[(firstNewline + 1)..lastFence].Trim();
    }

    private static string NormalizeLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "en" => "en",
        "fr" => "fr",
        "el" => "el",
        "ar" => "ar",
        _ => "ru"
    };

    private static string BookingSuggestion(string language) => NormalizeLanguage(language) switch
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
        return s.Contains("запис") || s.Contains("приём") || s.Contains("прием")
               || s.Contains("appointment") || s.Contains("book") || s.Contains("rendez")
               || s.Contains("ραντεβ") || s.Contains("κλείσ") || s.Contains("حجز") || s.Contains("موعد");
    }

    private static string SafeLogPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty>";
        var clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return Truncate(clean, 400);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
