using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentalClinic.Filters;

/// <summary>
/// Runtime guard for the legacy hard-coded proactive Denta messages.
/// AI-generated replies already pass through the structured safety prompt, but
/// proactive messages bypass Gemini entirely, so they need the same no-guarantees rule.
/// </summary>
public sealed class DentaProactiveSafetyFilter : IAsyncActionFilter
{
    private static readonly IReadOnlyDictionary<string, string> SafeMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Вижу, вы на странице о лечении каналов. Это звучит страшно, но у нас это абсолютно безболезненно 🤍 Есть вопросы?"] =
                "Вижу, вы на странице о лечении каналов. Это может тревожить, но врач заранее обсудит обезболивание и способы сделать лечение максимально комфортным 🤍 Есть вопросы?",
            ["I see you're on our root canal treatment page. It may sound scary, but with us it's completely painless 🤍 Any questions?"] =
                "I see you're on our root canal treatment page. It can sound worrying, but the dentist will discuss anesthesia and comfort options with you beforehand 🤍 Any questions?",
            ["Je vois que vous êtes sur notre page de traitement de canal. Cela peut sembler effrayant, mais chez nous c'est totalement indolore 🤍 Des questions ?"] =
                "Je vois que vous êtes sur notre page de traitement de canal. Cela peut sembler inquiétant, mais le dentiste vous expliquera à l’avance les options d’anesthésie et de confort 🤍 Des questions ?",
            ["Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Ίσως ακούγεται τρομακτικό, αλλά με εμάς είναι εντελώς ανώδυνο 🤍 Έχετε ερωτήσεις;"] =
                "Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Μπορεί να ακούγεται ανησυχητικό, αλλά ο οδοντίατρος θα συζητήσει εκ των προτέρων τις επιλογές αναισθησίας και άνεσης 🤍 Έχετε ερωτήσεις;",
            ["أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مخيفًا، لكنه غير مؤلم تمامًا لدينا 🤍 هل لديك أسئلة؟"] =
                "أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مقلقًا، لكن طبيب الأسنان سيناقش معك مسبقًا خيارات التخدير والراحة 🤍 هل لديك أسئلة؟",

            ["Изучаете удаление зубов? Расскажу чего ожидать и как проходит процедура без боли и страха."] =
                "Изучаете удаление зубов? Расскажу, чего ожидать от процедуры и как обычно контролируют боль и тревогу.",
            ["Looking into tooth extraction? I can tell you what to expect and how the procedure goes without pain or fear."] =
                "Looking into tooth extraction? I can tell you what to expect and how pain and anxiety are typically managed.",
            ["Vous vous renseignez sur l'extraction dentaire ? Je peux vous expliquer à quoi vous attendre et comment se déroule la procédure sans douleur ni crainte."] =
                "Vous vous renseignez sur l'extraction dentaire ? Je peux vous expliquer à quoi vous attendre et comment la douleur et l’anxiété sont habituellement prises en charge.",
            ["Εξετάζετε την εξαγωγή δοντιού; Μπορώ να σας πω τι να περιμένετε και πώς γίνεται η διαδικασία χωρίς πόνο και φόβο."] =
                "Εξετάζετε την εξαγωγή δοντιού; Μπορώ να σας πω τι να περιμένετε και πώς συνήθως αντιμετωπίζονται ο πόνος και το άγχος.",
            ["هل تبحث عن خلع الأسنان؟ يمكنني إخبارك بما يمكن توقعه وكيف يتم الإجراء دون ألم أو خوف."] =
                "هل تبحث عن خلع الأسنان؟ يمكنني إخبارك بما يمكن توقعه وكيف تتم عادةً السيطرة على الألم والقلق."
        };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/chat", StringComparison.OrdinalIgnoreCase)
            || !TryGetProactiveMessage(context, out _))
        {
            await next();
            return;
        }

        // ChatStream writes SSE directly instead of returning an ObjectResult.
        // Proactive SSE is tiny and immediate, so buffering just this special path
        // avoids weakening normal AI streaming while still applying the safety guard.
        if (path.EndsWith("/stream", StringComparison.OrdinalIgnoreCase))
        {
            var response = context.HttpContext.Response;
            var originalBody = response.Body;
            await using var buffer = new MemoryStream();
            response.Body = buffer;

            try
            {
                await next();
                buffer.Position = 0;
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var payload = await reader.ReadToEndAsync();
                var safePayload = SanitizeSse(payload);
                response.Body = originalBody;
                await originalBody.WriteAsync(Encoding.UTF8.GetBytes(safePayload));
            }
            finally
            {
                response.Body = originalBody;
            }

            return;
        }

        var executed = await next();
        if (executed.Result is not ObjectResult { Value: not null } objectResult)
            return;

        var node = JsonSerializer.SerializeToNode(objectResult.Value);
        if (node is not JsonObject obj || obj["reply"] is not JsonValue replyNode
            || !replyNode.TryGetValue<string>(out var reply))
            return;

        obj["reply"] = Sanitize(reply);
        objectResult.Value = obj;
    }

    public static string Sanitize(string text)
    {
        foreach (var pair in SafeMessages)
            text = text.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return text;
    }

    public static string SanitizeSse(string payload)
    {
        var lines = payload.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var output = new StringBuilder(payload.Length + 64);

        foreach (var line in lines)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                output.Append(line).Append('\n');
                continue;
            }

            var json = line[6..];
            try
            {
                var node = JsonNode.Parse(json);
                if (node is JsonObject obj && obj["delta"] is JsonValue deltaNode
                    && deltaNode.TryGetValue<string>(out var delta))
                {
                    obj["delta"] = Sanitize(delta);
                    output.Append("data: ").Append(obj.ToJsonString()).Append('\n');
                    continue;
                }
            }
            catch (JsonException)
            {
                // Keep malformed/non-JSON SSE lines untouched; the chat controller
                // owns the transport contract and this guard must never break it.
            }

            output.Append(line).Append('\n');
        }

        if (!payload.EndsWith('\n') && output.Length > 0)
            output.Length--;

        return output.ToString();
    }

    private static bool TryGetProactiveMessage(ActionExecutingContext context, out string message)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;
            var property = argument.GetType().GetProperty("Message");
            if (property?.GetValue(argument) is not string value) continue;

            message = value;
            return value.StartsWith("__proactive_", StringComparison.Ordinal);
        }

        message = string.Empty;
        return false;
    }
}
