using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Middleware;

/// <summary>
/// Structured Gemini chat for the public assistant. It intentionally intercepts only
/// POST /api/chat and /api/chat/stream; the legacy ChatController remains available
/// for TTS and admin analytics and as code-level backwards compatibility.
/// </summary>
public sealed class StructuredChatMiddleware
{
    private static readonly string[] Models = ["gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-3.5-flash"];
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "ru", "en", "fr", "el", "ar" };
    private static readonly Regex SafeSessionId = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StructuredChatMiddleware> _logger;

    public StructuredChatMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHttpClientFactory httpFactory,
        ILogger<StructuredChatMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, ChatKnowledgeService knowledge)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isChat = HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/chat", StringComparison.OrdinalIgnoreCase);
        var isStream = HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/chat/stream", StringComparison.OrdinalIgnoreCase);

        if (!isChat && !isStream)
        {
            await _next(context);
            return;
        }

        StructuredChatRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StructuredChatRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                context.RequestAborted);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(context, isStream, "Некорректный запрос", StatusCodes.Status400BadRequest);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            await WriteErrorAsync(context, isStream, Localized("empty", NormalizeLang(request?.Lang)), StatusCodes.Status400BadRequest);
            return;
        }

        Clamp(request);
        var lang = NormalizeLang(request.Lang);
        StructuredChatReply reply;

        if (TryProactive(request.Message, lang, out var proactive))
        {
            reply = proactive;
        }
        else if (TryEmergency(request.Message, lang, out var emergency))
        {
            reply = emergency;
        }
        else
        {
            reply = await GenerateStructuredReplyAsync(request, lang, knowledge, context.RequestAborted);
        }

        reply.StartBooking = reply.StartBooking || IsBookingIntent(request.Message);
        reply.Suggestions = reply.Suggestions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => Truncate(s.Trim(), 80)).Distinct().Take(3).ToList();
        reply.Links = reply.Links
            .Where(l => !string.IsNullOrWhiteSpace(l.Text) && IsSafeRelativeLink(l.Url))
            .Select(l => new StructuredChatLink { Text = Truncate(l.Text.Trim(), 80), Url = l.Url.Trim() })
            .GroupBy(l => l.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(2)
            .ToList();

        await LogExchangeAsync(context, db, request, lang, reply.Reply);

        if (isStream)
            await WriteStreamAsync(context, reply);
        else
            await WriteJsonAsync(context, reply, StatusCodes.Status200OK);
    }

    private async Task<StructuredChatReply> GenerateStructuredReplyAsync(
        StructuredChatRequest request,
        string lang,
        ChatKnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration["Gemini:ApiKey"]))
            return Failure(lang);

        var knowledgeBlock = await knowledge.GetKnowledgeBlockAsync();
        var contactsBlock = knowledge.GetContactsBlock();
        var systemPrompt = BuildSystemPrompt(lang, knowledgeBlock, contactsBlock);

        var history = (request.History ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h.Text))
            .TakeLast(10)
            .ToList();

        // Older frontend versions may include the current message as the last history
        // entry as well as in Message. Remove that duplicate before sending Gemini.
        if (history.Count > 0
            && history[^1].Role.Equals("user", StringComparison.OrdinalIgnoreCase)
            && string.Equals(history[^1].Text.Trim(), request.Message.Trim(), StringComparison.Ordinal))
        {
            history.RemoveAt(history.Count - 1);
        }

        var contents = history.Select(h => new
        {
            role = h.Role.Equals("bot", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
            parts = new[] { new { text = Truncate(h.Text, 800) } }
        }).Cast<object>().ToList();
        contents.Add(new { role = "user", parts = new[] { new { text = request.Message } } });

        var requestBody = JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new
            {
                temperature = 0.55,
                maxOutputTokens = 650,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        reply = new { type = "STRING" },
                        suggestions = new { type = "ARRAY", items = new { type = "STRING" } },
                        links = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    text = new { type = "STRING" },
                                    url = new { type = "STRING" }
                                },
                                required = new[] { "text", "url" }
                            }
                        },
                        startBooking = new { type = "BOOLEAN" },
                        urgent = new { type = "BOOLEAN" }
                    },
                    required = new[] { "reply", "suggestions", "links", "startBooking", "urgent" }
                }
            }
        });

        var client = _httpFactory.CreateClient();
        foreach (var model in Models)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            using var response = await client.PostAsync(
                url,
                new StringContent(requestBody, Encoding.UTF8, "application/json"),
                cancellationToken);

            if ((int)response.StatusCode is 404 or 429)
            {
                _logger.LogWarning("Gemini chat model {Model} unavailable ({Status})", model, (int)response.StatusCode);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini chat request failed ({Status}) for {Model}", (int)response.StatusCode, model);
                return Failure(lang);
            }

            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var text = doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                if (string.IsNullOrWhiteSpace(text)) continue;
                var parsed = JsonSerializer.Deserialize<StructuredChatReply>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Reply))
                    return parsed;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning("Gemini returned an invalid structured chat response for {Model}", model);
            }
        }

        return Failure(lang);
    }

    private static string BuildSystemPrompt(string lang, string knowledge, string contacts)
    {
        var languageName = lang switch
        {
            "en" => "English",
            "fr" => "French",
            "el" => "Greek",
            "ar" => "Arabic",
            _ => "Russian"
        };

        return $"""
You are Denta, the Dental Clinic AI assistant. Reply only in {languageName}.
Be warm and concise (normally 2-4 sentences). Never diagnose. Explain possibilities only as general information and recommend an appropriate dental professional.
If the user wants to make/book/schedule an appointment in ANY wording or language, set startBooking=true.
If symptoms may need urgent assessment, set urgent=true and clearly advise prompt professional care. Never claim certainty.
Links must be relative paths beginning with /pages/. Use no more than 2 links and 3 short suggestions.
Return only the JSON object required by the response schema; do not add markdown or free-form fields.

Clinic contacts:
{contacts}

Clinic services, doctors and prices:
{knowledge}

Allowed pages:
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

    private static bool TryEmergency(string message, string lang, out StructuredChatReply response)
    {
        var lower = message.ToLowerInvariant();
        var highRiskTerms = new[]
        {
            "не могу дыш", "трудно дыш", "не могу глот", "сильное кровотеч", "не останавливается кровь", "сильный отек лица", "отёк лица", "температура и отек",
            "cannot breathe", "difficulty breathing", "cannot swallow", "uncontrolled bleeding", "severe facial swelling", "fever and swelling",
            "difficulté à respirer", "ne peux pas respirer", "difficulté à avaler", "saignement incontrôlable", "gonflement important du visage",
            "δυσκολία στην αναπνοή", "δεν μπορώ να αναπνεύσω", "δυσκολία στην κατάποση", "σοβαρή αιμορραγία", "έντονο πρήξιμο",
            "صعوبة في التنفس", "لا أستطيع التنفس", "صعوبة في البلع", "نزيف شديد", "تورم شديد في الوجه"
        };

        if (!highRiskTerms.Any(lower.Contains))
        {
            response = null!;
            return false;
        }

        var text = lang switch
        {
            "en" => "These symptoms can require urgent in-person assessment. I can’t diagnose the cause here; if breathing or swallowing is difficult, bleeding is heavy and does not stop, or facial swelling is rapidly increasing, seek emergency medical/dental care now or call your local emergency service.",
            "fr" => "Ces symptômes peuvent nécessiter une évaluation urgente en personne. Je ne peux pas poser de diagnostic ici ; si vous avez du mal à respirer ou à avaler, si le saignement est abondant et ne s’arrête pas, ou si le gonflement du visage augmente rapidement, consultez immédiatement un service d’urgence médical/dentaire.",
            "el" => "Αυτά τα συμπτώματα μπορεί να χρειάζονται επείγουσα εξέταση από κοντά. Δεν μπορώ να θέσω διάγνωση εδώ· αν δυσκολεύεστε να αναπνεύσετε ή να καταπιείτε, η αιμορραγία είναι έντονη και δεν σταματά ή το πρήξιμο του προσώπου αυξάνεται γρήγορα, ζητήστε άμεσα επείγουσα ιατρική/οδοντιατρική βοήθεια.",
            "ar" => "قد تتطلب هذه الأعراض تقييماً عاجلاً وجهاً لوجه. لا يمكنني تشخيص السبب هنا؛ إذا كان التنفس أو البلع صعباً، أو كان النزيف شديداً ولا يتوقف، أو كان تورم الوجه يزداد بسرعة، فاطلب رعاية طبية/سنية طارئة فوراً أو اتصل بخدمة الطوارئ المحلية.",
            _ => "Такие симптомы могут требовать срочного очного осмотра. Я не могу поставить диагноз в чате; если трудно дышать или глотать, кровотечение сильное и не останавливается либо отёк лица быстро нарастает — обратитесь за неотложной медицинской/стоматологической помощью сейчас или вызовите местную экстренную службу."
        };

        response = new StructuredChatReply
        {
            Reply = text,
            Suggestions = lang switch
            {
                "en" => ["Clinic contacts"],
                "fr" => ["Contacts de la clinique"],
                "el" => ["Επικοινωνία με την κλινική"],
                "ar" => ["جهات اتصال العيادة"],
                _ => ["Контакты клиники"]
            },
            Links = [new StructuredChatLink { Text = lang == "ru" ? "Контакты клиники →" : "Clinic contacts →", Url = "/pages/contact.html" }],
            StartBooking = true,
            Urgent = true
        };
        return true;
    }

    private static bool TryProactive(string message, string lang, out StructuredChatReply response)
    {
        var keyToTopic = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__proactive_implants__"] = "implants",
            ["__proactive_cosmetic__"] = "cosmetic dentistry",
            ["__proactive_crowns__"] = "crowns",
            ["__proactive_canal__"] = "root canal treatment",
            ["__proactive_extraction__"] = "tooth extraction",
            ["__proactive_bridges__"] = "dental bridges",
            ["__proactive_prosthetics__"] = "dentures and prosthetics",
            ["__proactive_about__"] = "our clinic"
        };

        if (!keyToTopic.TryGetValue(message, out var topic))
        {
            response = null!;
            return false;
        }

        var text = lang switch
        {
            "en" => $"I see you’re looking at {topic}. What would you like to know — the procedure, price, doctor, or appointment options?",
            "fr" => $"Je vois que vous consultez des informations sur {topic}. Que souhaitez-vous savoir : la procédure, le tarif, le médecin ou la prise de rendez-vous ?",
            "el" => $"Βλέπω ότι διαβάζετε για {topic}. Τι θα θέλατε να μάθετε: διαδικασία, τιμή, γιατρό ή ραντεβού;",
            "ar" => $"أرى أنك تقرأ عن {topic}. ما الذي تريد معرفته: الإجراء أم السعر أم الطبيب أم حجز موعد؟",
            _ => $"Вижу, вы изучаете тему «{topic}». Что рассказать: о процедуре, цене, враче или записи на приём?"
        };

        response = new StructuredChatReply
        {
            Reply = text,
            Suggestions = lang switch
            {
                "en" => ["Price", "How it works", "Book appointment"],
                "fr" => ["Tarif", "Comment ça se passe", "Prendre rendez-vous"],
                "el" => ["Τιμή", "Πώς γίνεται", "Κλείσιμο ραντεβού"],
                "ar" => ["السعر", "كيف يتم", "حجز موعد"],
                _ => ["Цена", "Как проходит", "Записаться"]
            },
            Links = [],
            StartBooking = false,
            Urgent = false
        };
        return true;
    }

    private static bool IsBookingIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        string[] terms =
        [
            "записаться", "запишите", "запись на", "приём", "прием",
            "book appointment", "make an appointment", "schedule appointment", "book a visit",
            "prendre rendez-vous", "rendez-vous", "réserver une consultation",
            "κλείσω ραντεβού", "κλείσιμο ραντεβού", "ραντεβού",
            "حجز موعد", "أريد موعد", "موعد عند الطبيب"
        ];
        return terms.Any(lower.Contains);
    }

    private static bool IsSafeRelativeLink(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith("/pages/", StringComparison.Ordinal)
        && !url.Contains("..", StringComparison.Ordinal);

    private static string NormalizeLang(string? lang) =>
        lang is not null && Languages.Contains(lang) ? lang.ToLowerInvariant() : "ru";

    private static void Clamp(StructuredChatRequest request)
    {
        request.Message = Truncate(request.Message.Trim(), 800);
        request.SessionId = Truncate(request.SessionId?.Trim(), 64);
        if (request.History is { Count: > 12 })
            request.History = request.History.TakeLast(12).ToList();
        if (request.History is not null)
            foreach (var item in request.History)
                item.Text = Truncate(item.Text, 800);
    }

    private async Task LogExchangeAsync(HttpContext context, ApplicationDbContext db, StructuredChatRequest request, string lang, string botReply)
    {
        try
        {
            var sessionId = !string.IsNullOrWhiteSpace(request.SessionId) && SafeSessionId.IsMatch(request.SessionId)
                ? request.SessionId
                : Guid.NewGuid().ToString("N");
            var patientId = GetVerifiedPatientId(context.User);

            db.ChatMessageLogs.AddRange(
                new ChatMessageLog
                {
                    SessionId = sessionId,
                    PatientId = patientId,
                    Role = "user",
                    Text = Truncate(request.Message, 1000),
                    Lang = lang,
                    // Stage 3 privacy policy: do not persist raw client IPs for new chat rows.
                    ClientIp = null
                },
                new ChatMessageLog
                {
                    SessionId = sessionId,
                    PatientId = patientId,
                    Role = "bot",
                    Text = Truncate(botReply, 1000),
                    Lang = lang,
                    ClientIp = null
                });
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat response succeeded but analytics log write failed");
        }
    }

    private static int? GetVerifiedPatientId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Patient")) return null;
        return int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static StructuredChatReply Failure(string lang) => new()
    {
        Reply = Localized("error", lang),
        Suggestions = [],
        Links = [],
        StartBooking = false,
        Urgent = false
    };

    private static string Localized(string key, string lang) => (key, lang) switch
    {
        ("empty", "en") => "Please enter a message.",
        ("empty", "fr") => "Veuillez saisir un message.",
        ("empty", "el") => "Παρακαλώ γράψτε ένα μήνυμα.",
        ("empty", "ar") => "يرجى كتابة رسالة.",
        ("empty", _) => "Введите сообщение.",
        ("error", "en") => "AI is temporarily unavailable. Please try again later.",
        ("error", "fr") => "L’IA est temporairement indisponible. Réessayez plus tard.",
        ("error", "el") => "Η AI δεν είναι προσωρινά διαθέσιμη. Δοκιμάστε ξανά αργότερα.",
        ("error", "ar") => "الذكاء الاصطناعي غير متاح مؤقتاً. حاول مرة أخرى لاحقاً.",
        _ => "AI временно недоступен. Попробуйте позже."
    };

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];

    private static async Task WriteJsonAsync(HttpContext context, object value, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(context.Response.Body, value, value.GetType(), cancellationToken: context.RequestAborted);
    }

    private static async Task WriteErrorAsync(HttpContext context, bool stream, string message, int statusCode)
    {
        if (!stream)
        {
            await WriteJsonAsync(context, new StructuredChatReply { Reply = message }, statusCode);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(new { error = message, done = true })}\n\n", context.RequestAborted);
    }

    private static async Task WriteStreamAsync(HttpContext context, StructuredChatReply reply)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(new { delta = reply.Reply })}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(new
        {
            done = true,
            suggestions = reply.Suggestions,
            links = reply.Links,
            startBooking = reply.StartBooking,
            urgent = reply.Urgent
        })}\n\n", context.RequestAborted);
    }
}

public sealed class StructuredChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<StructuredChatHistoryItem>? History { get; set; }
    public string Lang { get; set; } = "ru";
    public string? SessionId { get; set; }
    public int? PatientId { get; set; } // compatibility only; identity comes from JWT cookie/bearer
}

public sealed class StructuredChatHistoryItem
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;
}

public sealed class StructuredChatReply
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];

    [JsonPropertyName("links")]
    public List<StructuredChatLink> Links { get; set; } = [];

    [JsonPropertyName("startBooking")]
    public bool StartBooking { get; set; }

    [JsonPropertyName("urgent")]
    public bool Urgent { get; set; }
}

public sealed class StructuredChatLink
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
