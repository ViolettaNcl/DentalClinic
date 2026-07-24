using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using System.Text;
using System.Text.Json;
using System.Security.Claims;

namespace DentalClinic.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ChatKnowledgeService _knowledge;

        // gemini-2.0-flash и gemini-2.0-flash-lite отключены Google с 1 июня 2026 —
        // держим актуальный список стабильных моделей, от самой умной к самой дешёвой,
        // чтобы при перегрузке/квоте одной модели бот автоматически падал на следующую
        private static readonly string[] GeminiModels =
            { "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-3.5-flash" };

        private static readonly Dictionary<string[], (string url, string name)> PageKeywords = new()
        {
            { new[]{"имплант","all-on"}, ("/pages/services/implants.html", "Подробнее об имплантах →") },
            { new[]{"коронк"}, ("/pages/services/crowns.html", "Подробнее о коронках →") },
            { new[]{"пломб"}, ("/pages/services/fillings.html", "Подробнее о пломбах →") },
            { new[]{"канал","нерв","эндодонт"}, ("/pages/services/root-canal.html", "Лечение каналов →") },
            { new[]{"удален","экстракц","зуб мудрост"}, ("/pages/services/extractions.html", "Удаление зубов →") },
            { new[]{"мост"}, ("/pages/services/bridges.html", "Подробнее о мостах →") },
            { new[]{"протез"}, ("/pages/services/prosthetics.html", "Подробнее о протезах →") },
            { new[]{"косметик","отбелив","винир","улыбк"}, ("/pages/services/cosmetic-treatments.html", "Косметические процедуры →") },
            { new[]{"врач","доктор","специалист"}, ("/pages/doctors.html", "Наши врачи →") },
            { new[]{"записат","контакт","приём"}, ("/pages/contact.html", "Записаться онлайн →") },
            { new[]{"о клинике","сертифик","философ"}, ("/pages/about.html", "О клинике →") },
        };

        // Проактивные сообщения — бот сам инициирует разговор.
        // Раньше это был Dictionary<string,string> только с русским текстом,
        // из-за чего проактивные сообщения всегда приходили на русском независимо
        // от языка сайта. Теперь это словарь словарей: ключ триггера → язык → текст.
        private static readonly Dictionary<string, Dictionary<string, string>> ProactiveMessages = new()
        {
            ["__proactive_implants__"] = new()
            {
                ["ru"] = "Замечаю, что вы изучаете страницу об имплантах. Есть вопросы? Расскажу всё о процедуре, ценах и враче Dr. Лилит Рейнер 😊",
                ["en"] = "I notice you're looking at our implants page. Any questions? I can tell you all about the procedure, prices and Dr. Lilith Reiner 😊",
                ["fr"] = "Je vois que vous consultez notre page sur les implants. Des questions ? Je peux vous parler de la procédure, des tarifs et du Dr Lilith Reiner 😊",
                ["el"] = "Βλέπω ότι εξετάζετε τη σελίδα για τα εμφυτεύματα. Έχετε ερωτήσεις; Μπορώ να σας πω για τη διαδικασία, τις τιμές και τη Δρ. Lilith Reiner 😊",
                ["ar"] = "ألاحظ أنك تتصفح صفحة الزراعات. هل لديك أسئلة؟ يمكنني إخبارك عن الإجراء والأسعار والدكتورة ليليث راينر 😊",
            },
            ["__proactive_cosmetic__"] = new()
            {
                ["ru"] = "Интересуетесь косметической стоматологией? Могу рассказать об отбеливании, винирах и дизайне улыбки — что интересует больше всего?",
                ["en"] = "Interested in cosmetic dentistry? I can tell you about whitening, veneers and smile design — what interests you most?",
                ["fr"] = "Intéressé(e) par la dentisterie esthétique ? Je peux vous parler du blanchiment, des facettes et du design du sourire — qu'est-ce qui vous intéresse le plus ?",
                ["el"] = "Ενδιαφέρεστε για αισθητική οδοντιατρική; Μπορώ να σας πω για λεύκανση, όψεις και σχεδιασμό χαμόγελου — τι σας ενδιαφέρει περισσότερο;",
                ["ar"] = "هل تهتم بطب الأسنان التجميلي؟ يمكنني إخبارك عن التبييض والقشور وتصميم الابتسامة — ما الذي يهمك أكثر؟",
            },
            ["__proactive_crowns__"] = new()
            {
                ["ru"] = "Изучаете коронки? Помогу выбрать между металлокерамикой и цирконием — расскажите что важнее: цена или эстетика?",
                ["en"] = "Looking into crowns? I can help you choose between metal-ceramic and zirconia — what matters more to you: price or aesthetics?",
                ["fr"] = "Vous vous renseignez sur les couronnes ? Je peux vous aider à choisir entre céramo-métallique et zircone — qu'est-ce qui compte le plus : le prix ou l'esthétique ?",
                ["el"] = "Εξετάζετε τα στεφάνια; Μπορώ να σας βοηθήσω να επιλέξετε μεταξύ μεταλλοκεραμικού και ζιρκονίου — τι έχει μεγαλύτερη σημασία: η τιμή ή η αισθητική;",
                ["ar"] = "هل تبحث عن التيجان؟ يمكنني مساعدتك في الاختيار بين الخزف المعدني والزركونيا — ما الأهم بالنسبة لك: السعر أم المظهر؟",
            },
            ["__proactive_canal__"] = new()
            {
                ["ru"] = "Вижу, вы на странице о лечении каналов. Это звучит страшно, но у нас это абсолютно безболезненно 🤍 Есть вопросы?",
                ["en"] = "I see you're on our root canal treatment page. It may sound scary, but with us it's completely painless 🤍 Any questions?",
                ["fr"] = "Je vois que vous êtes sur notre page de traitement de canal. Cela peut sembler effrayant, mais chez nous c'est totalement indolore 🤍 Des questions ?",
                ["el"] = "Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Ίσως ακούγεται τρομακτικό, αλλά με εμάς είναι εντελώς ανώδυνο 🤍 Έχετε ερωτήσεις;",
                ["ar"] = "أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مخيفًا، لكنه غير مؤلم تمامًا لدينا 🤍 هل لديك أسئلة؟",
            },
            ["__proactive_extraction__"] = new()
            {
                ["ru"] = "Изучаете удаление зубов? Расскажу чего ожидать и как проходит процедура без боли и страха.",
                ["en"] = "Looking into tooth extraction? I can tell you what to expect and how the procedure goes without pain or fear.",
                ["fr"] = "Vous vous renseignez sur l'extraction dentaire ? Je peux vous expliquer à quoi vous attendre et comment se déroule la procédure sans douleur ni crainte.",
                ["el"] = "Εξετάζετε την εξαγωγή δοντιού; Μπορώ να σας πω τι να περιμένετε και πώς γίνεται η διαδικασία χωρίς πόνο και φόβο.",
                ["ar"] = "هل تبحث عن خلع الأسنان؟ يمكنني إخبارك بما يمكن توقعه وكيف يتم الإجراء دون ألم أو خوف.",
            },
            ["__proactive_bridges__"] = new()
            {
                ["ru"] = "Интересуетесь мостами? Помогу понять какой вариант подойдёт именно вам — есть вопросы?",
                ["en"] = "Interested in dental bridges? I can help you find out which option suits you best — any questions?",
                ["fr"] = "Intéressé(e) par les bridges dentaires ? Je peux vous aider à trouver l'option qui vous convient le mieux — des questions ?",
                ["el"] = "Ενδιαφέρεστε για γέφυρες δοντιών; Μπορώ να σας βοηθήσω να βρείτε ποια επιλογή σας ταιριάζει καλύτερα — έχετε ερωτήσεις;",
                ["ar"] = "هل تهتم بالجسور السنية؟ يمكنني مساعدتك في معرفة الخيار الأنسب لك — هل لديك أسئلة؟",
            },
            ["__proactive_prosthetics__"] = new()
            {
                ["ru"] = "Изучаете протезы? Расскажу о разнице между акриловыми, нейлоновыми и бюгельными — что важнее: комфорт или бюджет?",
                ["en"] = "Looking into dentures? I can explain the difference between acrylic, nylon and partial dentures — what matters more: comfort or budget?",
                ["fr"] = "Vous vous renseignez sur les prothèses dentaires ? Je peux vous expliquer la différence entre les prothèses acryliques, en nylon et partielles — qu'est-ce qui compte le plus : le confort ou le budget ?",
                ["el"] = "Εξετάζετε τις οδοντοστοιχίες; Μπορώ να εξηγήσω τη διαφορά μεταξύ ακρυλικών, νάιλον και μερικών οδοντοστοιχιών — τι έχει μεγαλύτερη σημασία: η άνεση ή ο προϋπολογισμός;",
                ["ar"] = "هل تبحث عن أطقم الأسنان؟ يمكنني شرح الفرق بين الأكريليك والنايلون والأطقم الجزئية — ما الأهم: الراحة أم الميزانية؟",
            },
            ["__proactive_about__"] = new()
            {
                ["ru"] = "Изучаете нашу клинику? Могу рассказать о наших врачах, сертификатах или ответить на любые вопросы 😊",
                ["en"] = "Learning more about our clinic? I can tell you about our doctors, certifications, or answer any questions 😊",
                ["fr"] = "Vous en apprenez plus sur notre clinique ? Je peux vous parler de nos médecins, de nos certifications ou répondre à vos questions 😊",
                ["el"] = "Μαθαίνετε περισσότερα για την κλινική μας; Μπορώ να σας πω για τους γιατρούς μας, τα πιστοποιητικά μας ή να απαντήσω σε ερωτήσεις 😊",
                ["ar"] = "هل تتعرف أكثر على عيادتنا؟ يمكنني إخبارك عن أطبائنا وشهاداتنا أو الإجابة عن أي أسئلة 😊",
            },
            ["__proactive_doctors__"] = new()
            {
                ["ru"] = "Хотите узнать больше о наших врачах? Расскажу об их специализации и опыте.",
                ["en"] = "Want to know more about our doctors? I can tell you about their specialization and experience.",
                ["fr"] = "Vous voulez en savoir plus sur nos médecins ? Je peux vous parler de leur spécialisation et de leur expérience.",
                ["el"] = "Θέλετε να μάθετε περισσότερα για τους γιατρούς μας; Μπορώ να σας πω για την εξειδίκευση και την εμπειρία τους.",
                ["ar"] = "هل تريد معرفة المزيد عن أطبائنا؟ يمكنني إخبارك عن تخصصهم وخبرتهم.",
            },
            ["__proactive_home__"] = new()
            {
                ["ru"] = "Добро пожаловать в Dental Clinic! 👋 Я Дента — ваш AI-ассистент. Помогу с выбором услуги, расскажу о ценах или запишу на приём.",
                ["en"] = "Welcome to Dental Clinic! 👋 I'm Denta — your AI assistant. I can help you choose a service, tell you about prices, or book an appointment.",
                ["fr"] = "Bienvenue chez Dental Clinic ! 👋 Je suis Denta, votre assistante IA. Je peux vous aider à choisir un service, vous parler des tarifs ou prendre rendez-vous.",
                ["el"] = "Καλώς ήρθατε στο Dental Clinic! 👋 Είμαι η Denta, ο AI βοηθός σας. Μπορώ να σας βοηθήσω να επιλέξετε υπηρεσία, να σας πω για τις τιμές ή να κλείσω ραντεβού.",
                ["ar"] = "مرحبًا بك في Dental Clinic! 👋 أنا دنتا، مساعدتك الذكية. يمكنني مساعدتك في اختيار خدمة، وإخبارك بالأسعار، أو حجز موعد.",
            },
        };

        // Кнопки-подсказки под проактивным сообщением — тоже должны быть на языке сайта.
        private static readonly Dictionary<string, string[]> ProactiveSuggestions = new()
        {
            ["ru"] = new[] { "Расскажите подробнее", "Сколько это стоит?", "Записаться на приём" },
            ["en"] = new[] { "Tell me more", "How much does it cost?", "Book an appointment" },
            ["fr"] = new[] { "En savoir plus", "Quel est le prix ?", "Prendre rendez-vous" },
            ["el"] = new[] { "Πείτε μου περισσότερα", "Πόσο κοστίζει;", "Κλείστε ραντεβού" },
            ["ar"] = new[] { "أخبرني المزيد", "كم تبلغ التكلفة؟", "احجز موعدًا" },
        };

        private static readonly Dictionary<string, string> ErrEmpty = new()
        {
            ["ru"] = "Пустое сообщение.",
            ["en"] = "Empty message.",
            ["fr"] = "Message vide.",
            ["el"] = "Κενό μήνυμα.",
            ["ar"] = "رسالة فارغة."
        };
        private static readonly Dictionary<string, string> ErrNoKey = new()
        {
            ["ru"] = "API-ключ не настроен.",
            ["en"] = "API key not configured.",
            ["fr"] = "Clé API non configurée.",
            ["el"] = "Κλειδί API δεν ρυθμίστηκε.",
            ["ar"] = "مفتاح API غير مهيأ."
        };
        private static readonly Dictionary<string, string> ErrAi = new()
        {
            ["ru"] = "Ошибка AI. Попробуйте позже.",
            ["en"] = "AI error. Try again later.",
            ["fr"] = "Erreur IA. Réessayez plus tard.",
            ["el"] = "Σφάλμα AI. Δοκιμάστε αργότερα.",
            ["ar"] = "خطأ AI. حاول لاحقًا."
        };
        private static readonly Dictionary<string, string> ErrOverloaded = new()
        {
            ["ru"] = "Сервис временно перегружен. Подождите минуту.",
            ["en"] = "Service overloaded. Please wait a minute.",
            ["fr"] = "Service surchargé. Patientez une minute.",
            ["el"] = "Υπηρεσία υπερφορτωμένη. Περιμένετε λίγο.",
            ["ar"] = "الخدمة مثقلة. انتظر دقيقة."
        };
        private static readonly Dictionary<string, string> LangNames = new()
        {
            ["ru"] = "русском",
            ["en"] = "английском (English)",
            ["fr"] = "французском (français)",
            ["el"] = "греческом (ελληνικά)",
            ["ar"] = "арабском (العربية)"
        };

        private static string L(Dictionary<string, string> d, string lang) => d.TryGetValue(lang, out var v) ? v : d["ru"];
        private static string[] L(Dictionary<string, string[]> d, string lang) => d.TryGetValue(lang, out var v) ? v : d["ru"];

        public ChatController(
            IConfiguration config,
            IHttpClientFactory httpFactory,
            ILogger<ChatController> logger,
            ApplicationDbContext db,
            ChatKnowledgeService knowledge)
        {
            _config = config;
            _http = httpFactory.CreateClient();
            _logger = logger;
            _db = db;
            _knowledge = knowledge;
        }

        // ═══════════════════════════════════════════════════════════
        //  Обычный (не потоковый) чат — оставлен для обратной совместимости
        //  и как фолбэк, если у клиента не работает SSE.
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [EnableRateLimiting("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest req)
        {
            var lang = NormalizeLang(req.Lang);

            if (string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { reply = L(ErrEmpty, lang), suggestions = Array.Empty<string>(), links = Array.Empty<object>() });

            ClampLengths(req);

            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return StatusCode(500, new { reply = L(ErrNoKey, lang), suggestions = Array.Empty<string>(), links = Array.Empty<object>() });

            // ── Проактивное сообщение — отвечаем напрямую без AI ──
            if (ProactiveMessages.TryGetValue(req.Message, out var proMsgByLang))
            {
                // Ссылки определяются по ключевым словам в русском варианте текста —
                // так автоопределение страницы работает независимо от языка ответа.
                var proLinks = AutoLinks(L(proMsgByLang, "ru").ToLower());
                return Ok(new
                {
                    reply = L(proMsgByLang, lang),
                    suggestions = L(ProactiveSuggestions, lang),
                    links = proLinks
                });
            }

            var systemPrompt = await BuildSystemPromptAsync(lang);
            var contents = BuildContents(req);
            var body = BuildRequestBody(systemPrompt, contents);

            foreach (var model in GeminiModels)
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                var raw = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 429 || (int)response.StatusCode == 404)
                {
                    _logger.LogWarning("Gemini модель {Model} недоступна ({Status}), пробуем следующую", model, (int)response.StatusCode);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ошибка Gemini API ({Status}) для модели {Model}: {Body}", (int)response.StatusCode, model, raw);
                    return StatusCode(500, new { reply = L(ErrAi, lang), suggestions = Array.Empty<string>(), links = Array.Empty<object>() });
                }

                using var doc = JsonDocument.Parse(raw);
                var fullText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                var (reply, suggestions, links) = ParseModelOutput(fullText);
                var combined = (req.Message + " " + reply).ToLower();
                var startBooking = combined.Contains("записат") || combined.Contains("приём") || combined.Contains("запись");
                if (links.Count == 0) links = AutoLinks(combined);

                await LogExchangeAsync(req, lang, reply);

                return Ok(new { reply, suggestions, links, startBooking });
            }

            _logger.LogError("Все модели Gemini недоступны или перегружены");
            return StatusCode(429, new { reply = L(ErrOverloaded, lang), suggestions = Array.Empty<string>(), links = Array.Empty<object>(), startBooking = false });
        }

        // ═══════════════════════════════════════════════════════════
        //  Потоковый чат (SSE) — печатает ответ по мере генерации,
        //  вместо ожидания всего ответа целиком.
        //  Клиент читает событие через fetch()+ReadableStream (не EventSource,
        //  т.к. запрос идёт через POST с телом).
        // ═══════════════════════════════════════════════════════════
        [HttpPost("stream")]
        [EnableRateLimiting("chat")]
        public async Task ChatStream([FromBody] ChatRequest req)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // отключаем буферизацию на nginx, если он стоит перед Kestrel

            var lang = NormalizeLang(req.Lang);

            async Task SendAsync(object payload)
            {
                var json = JsonSerializer.Serialize(payload);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }

            if (string.IsNullOrWhiteSpace(req.Message))
            {
                await SendAsync(new { error = L(ErrEmpty, lang), done = true });
                return;
            }

            ClampLengths(req);

            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                await SendAsync(new { error = L(ErrNoKey, lang), done = true });
                return;
            }

            if (ProactiveMessages.TryGetValue(req.Message, out var proMsgByLang))
            {
                var proReply = L(proMsgByLang, lang);
                await SendAsync(new { delta = proReply });
                await SendAsync(new
                {
                    done = true,
                    suggestions = L(ProactiveSuggestions, lang),
                    // Ссылки определяются по ключевым словам в русском варианте текста —
                    // так автоопределение страницы работает независимо от языка ответа.
                    links = AutoLinks(L(proMsgByLang, "ru").ToLower()),
                    startBooking = false
                });
                return;
            }

            var systemPrompt = await BuildSystemPromptAsync(lang);
            var contents = BuildContents(req);
            var body = BuildRequestBody(systemPrompt, contents);

            foreach (var model in GeminiModels)
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse&key={apiKey}";

                using var upstreamReq = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage upstreamResp;
                try
                {
                    upstreamResp = await _http.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось подключиться к Gemini stream API (модель {Model})", model);
                    continue;
                }

                if ((int)upstreamResp.StatusCode == 429 || (int)upstreamResp.StatusCode == 404)
                {
                    _logger.LogWarning("Gemini модель {Model} недоступна ({Status}) в стриме, пробуем следующую", model, (int)upstreamResp.StatusCode);
                    upstreamResp.Dispose();
                    continue;
                }
                if (!upstreamResp.IsSuccessStatusCode)
                {
                    var errBody = await upstreamResp.Content.ReadAsStringAsync();
                    _logger.LogError("Ошибка Gemini stream API ({Status}) для модели {Model}: {Body}", (int)upstreamResp.StatusCode, model, errBody);
                    upstreamResp.Dispose();
                    await SendAsync(new { error = L(ErrAi, lang), done = true });
                    return;
                }

                var fullText = new StringBuilder();
                var sentLength = 0;
                var markerFound = false;

                await using var stream = await upstreamResp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!line.StartsWith("data:")) continue;
                    var payload = line[5..].Trim();
                    if (string.IsNullOrEmpty(payload) || payload == "[DONE]") continue;

                    string? textPart = null;
                    try
                    {
                        using var chunkDoc = JsonDocument.Parse(payload);
                        textPart = chunkDoc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();
                    }
                    catch
                    {
                        continue; // неполный/незначимый чанк — пропускаем
                    }

                    if (string.IsNullOrEmpty(textPart)) continue;
                    fullText.Append(textPart);

                    if (markerFound) continue;

                    var current = fullText.ToString();
                    var markerIdx = current.IndexOf("SUGGESTIONS:", StringComparison.Ordinal);
                    if (markerIdx >= 0)
                    {
                        markerFound = true;
                        var safe = current[..markerIdx];
                        if (safe.Length > sentLength)
                        {
                            await SendAsync(new { delta = safe[sentLength..] });
                            sentLength = safe.Length;
                        }
                    }
                    else
                    {
                        // Маркер SUGGESTIONS: ещё не встретился целиком, но хвост текста
                        // может оказаться его началом (например текст обрывается на "...SUG").
                        // Такой хвост не отправляем — иначе кусок маркера "утечёт" в чат,
                        // как только следующий фрагмент допишет слово до конца.
                        var safeLen = SafeSendLength(current);
                        if (safeLen > sentLength)
                        {
                            await SendAsync(new { delta = current[sentLength..safeLen] });
                            sentLength = safeLen;
                        }
                    }
                }

                var (reply, suggestions, links) = ParseModelOutput(fullText.ToString());

                // Если модель ни разу не прислала маркер SUGGESTIONS (редкий случай
                // обрыва потока) — досылаем остаток текста, чтобы пользователь не
                // потерял хвост ответа.
                if (!markerFound && reply.Length > sentLength)
                    await SendAsync(new { delta = reply[sentLength..] });

                var combined = (req.Message + " " + reply).ToLower();
                var startBooking = combined.Contains("записат") || combined.Contains("приём") || combined.Contains("запись");
                if (links.Count == 0) links = AutoLinks(combined);

                await SendAsync(new { done = true, suggestions, links, startBooking });
                await LogExchangeAsync(req, lang, reply);
                return;
            }

            _logger.LogError("Все модели Gemini недоступны или перегружены (стрим)");
            await SendAsync(new { error = L(ErrOverloaded, lang), done = true });
        }

        // ═══════════════════════════════════════════════════════════
        //  Озвучка ответа настоящим нейросетевым голосом (ElevenLabs) —
        //  вместо системного голоса браузера. Бэкенд проксирует запрос,
        //  чтобы API-ключ ElevenLabs не светился в JS на клиенте.
        //  Если ключ не настроен — отдаём 204, фронт сам откатится на
        //  голос браузера (см. chatBot.js: _speakWithBrowser).
        // ═══════════════════════════════════════════════════════════
        [HttpPost("tts")]
        [EnableRateLimiting("chat")]
        public async Task Speak([FromBody] TtsRequest req)
        {
            var apiKey = _config["ElevenLabs:ApiKey"];
            var voiceId = _config["ElevenLabs:VoiceId"];
            if (string.IsNullOrWhiteSpace(voiceId))
                voiceId = "21m00Tcm4TlvDq8ikWAM"; // "Rachel" — голос ElevenLabs по умолчанию

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(req.Text))
            {
                Response.StatusCode = 204;
                return;
            }

            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
            var body = JsonSerializer.Serialize(new
            {
                text = Truncate(req.Text, 600),
                model_id = "eleven_multilingual_v2", // сам определяет язык из текста — отдельная модель на язык не нужна
                voice_settings = new { stability = 0.5, similarity_boost = 0.8 }
            });

            using var upstreamReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            upstreamReq.Headers.Add("xi-api-key", apiKey);
            upstreamReq.Headers.Add("Accept", "audio/mpeg");

            HttpResponseMessage upstreamResp;
            try
            {
                upstreamResp = await _http.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подключиться к ElevenLabs TTS");
                Response.StatusCode = 502;
                return;
            }

            if (!upstreamResp.IsSuccessStatusCode)
            {
                var errBody = await upstreamResp.Content.ReadAsStringAsync();
                _logger.LogWarning("Ошибка ElevenLabs TTS ({Status}): {Body}", (int)upstreamResp.StatusCode, errBody);
                upstreamResp.Dispose();
                Response.StatusCode = 502;
                return;
            }

            Response.ContentType = "audio/mpeg";
            await using var audioStream = await upstreamResp.Content.ReadAsStreamAsync();
            await audioStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
        }

        // ═══════════════════════════════════════════════════════════
        //  Админ: аналитика по чату — видно о чём спрашивают пациенты
        // ═══════════════════════════════════════════════════════════

        // Последние диалоги целиком, сгруппированные по сессии
        [HttpGet("admin/sessions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRecentSessions([FromQuery] int take = 50)
        {
            take = Math.Clamp(take, 1, 200);

            var recentSessionIds = await _db.ChatMessageLogs
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.SessionId)
                .Distinct()
                .Take(take)
                .ToListAsync();

            var logs = await _db.ChatMessageLogs
                .Where(m => recentSessionIds.Contains(m.SessionId))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var sessions = logs
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    sessionId = g.Key,
                    startedAt = g.Min(m => m.CreatedAt),
                    patientId = g.FirstOrDefault(m => m.PatientId != null)?.PatientId,
                    messageCount = g.Count(),
                    preview = g.FirstOrDefault(m => m.Role == "user")?.Text ?? g.First().Text,
                    messages = g.Select(m => new { m.Role, m.Text, m.CreatedAt })
                })
                .OrderByDescending(s => s.startedAt)
                .ToList();

            return Ok(sessions);
        }

        // Агрегированная статистика: сколько сообщений/диалогов, и какие темы
        // спрашивают чаще всего (по тем же ключевым словам, что используются
        // для авто-ссылок) — прямая подсказка какие услуги продвигать.
        [HttpGet("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStats([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 365);
            var since = DateTime.UtcNow.AddDays(-days);

            var userMessages = await _db.ChatMessageLogs
                .Where(m => m.Role == "user" && m.CreatedAt >= since)
                .Select(m => new { m.Text, m.CreatedAt })
                .ToListAsync();

            var totalSessions = await _db.ChatMessageLogs
                .Where(m => m.CreatedAt >= since)
                .Select(m => m.SessionId)
                .Distinct()
                .CountAsync();

            var topics = PageKeywords
                .Select(kv => new
                {
                    topic = kv.Value.name.Replace(" →", ""),
                    count = userMessages.Count(m => kv.Key.Any(kw => m.Text.ToLower().Contains(kw)))
                })
                .Where(t => t.count > 0)
                .OrderByDescending(t => t.count)
                .ToList();

            var byDay = userMessages
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
                .OrderBy(g => g.date)
                .ToList();

            return Ok(new
            {
                totalMessages = userMessages.Count,
                totalSessions,
                topics,
                byDay
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  Вспомогательные методы
        // ═══════════════════════════════════════════════════════════

        private static string NormalizeLang(string? lang) => string.IsNullOrWhiteSpace(lang) ? "ru" : lang.ToLowerInvariant();

        private const string SuggestionsMarker = "SUGGESTIONS:";

        // Возвращает длину префикса text, который точно безопасно отдавать
        // клиенту в потоке — т.е. хвост, который потенциально может быть
        // недописанным началом маркера "SUGGESTIONS:", отрезается и придерживается
        // до следующего чанка.
        private static int SafeSendLength(string text)
        {
            var maxOverlap = Math.Min(SuggestionsMarker.Length - 1, text.Length);
            for (var len = maxOverlap; len > 0; len--)
            {
                if (text.EndsWith(SuggestionsMarker[..len], StringComparison.Ordinal))
                    return text.Length - len;
            }
            return text.Length;
        }

        // Ограничиваем длину сообщения и истории — иначе один запрос можно
        // раздуть до огромного количества токенов и накрутить счёт за AI API.
        private static void ClampLengths(ChatRequest req)
        {
            if (req.Message.Length > 800)
                req.Message = req.Message[..800];

            if (req.History != null)
            {
                if (req.History.Count > 12)
                    req.History = req.History.Skip(req.History.Count - 12).ToList();

                foreach (var msg in req.History)
                    if (msg.Text?.Length > 800)
                        msg.Text = msg.Text[..800];
            }
        }

        private async Task<string> BuildSystemPromptAsync(string lang)
        {
            var langName = LangNames.TryGetValue(lang, out var ln) ? ln : LangNames["ru"];
            var knowledgeBlock = await _knowledge.GetKnowledgeBlockAsync();
            var contactsBlock = _knowledge.GetContactsBlock();

            return
                "Ты — Дента, умный AI-ассистент стоматологической клиники Dental Clinic (Волгоград).\n" +
                "\n" +
                "СТИЛЬ: коротко (2-3 предложения), тепло, по делу.\n" +
                $"ЯЗЫК: отвечай ТОЛЬКО на {langName}. Кнопки SUGGESTIONS тоже на {langName}.\n" +
                "\n" +
                "АНАЛИЗ СИМПТОМОВ — если пациент описывает боль или проблему:\n" +
                "1. Посочувствуй коротко\n" +
                "2. Задай 1 уточняющий вопрос (давно? острая/тупая? температурная реакция?)\n" +
                "3. После уточнения — порекомендуй услугу\n" +
                "Примеры: боль при жевании → вероятно кариес/пломба; боль от холодного → каналы; опухоль → срочно к врачу\n" +
                "Никогда не ставь диагноз — только рекомендуй обратиться!\n" +
                "\n" +
                "ЗАПИСЬ: если пациент хочет записаться — добавь startBooking:true в ответ (отдельным полем)\n" +
                "\n" +
                "ФОРМАТ — строго три блока:\n" +
                "Текст (2-3 предложения)\n" +
                "SUGGESTIONS:[\"кнопка1\",\"кнопка2\",\"кнопка3\"]\n" +
                "LINKS:[{\"text\":\"Название →\",\"url\":\"/pages/...\"}] или LINKS:[]\n" +
                "\n" +
                "=== ДАННЫЕ КЛИНИКИ ===\n" +
                contactsBlock + "\n" +
                "Технологии: ИИ-диагностика, цифровой рентген, лазер, 3D-сканирование, седация\n" +
                "\n" +
                knowledgeBlock +
                "\n" +
                "СТРАНИЦЫ:\n" +
                "/pages/services/implants.html /pages/services/crowns.html /pages/services/fillings.html\n" +
                "/pages/services/root-canal.html /pages/services/extractions.html /pages/services/bridges.html\n" +
                "/pages/services/prosthetics.html /pages/services/cosmetic-treatments.html\n" +
                "/pages/doctors.html /pages/contact.html /pages/about.html\n";
        }

        private static List<object> BuildContents(ChatRequest req)
        {
            var contents = new List<object>();
            if (req.History != null)
                foreach (var msg in req.History)
                    contents.Add(new { role = msg.Role == "bot" ? "model" : "user", parts = new[] { new { text = msg.Text } } });
            contents.Add(new { role = "user", parts = new[] { new { text = req.Message } } });
            return contents;
        }

        private static string BuildRequestBody(string systemPrompt, List<object> contents) => JsonSerializer.Serialize(new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { temperature = 0.75, maxOutputTokens = 600 }
        });

        // Разбирает "Текст\nSUGGESTIONS:[...]\nLINKS:[...]" из сырого ответа модели
        private static (string reply, List<string> suggestions, List<Dictionary<string, string>> links) ParseModelOutput(string fullText)
        {
            var reply = fullText;
            var suggestions = new List<string>();
            var links = new List<Dictionary<string, string>>();

            var idxS = fullText.IndexOf("SUGGESTIONS:", StringComparison.Ordinal);
            if (idxS >= 0)
            {
                reply = fullText[..idxS].Trim();
                var afterS = fullText[(idxS + 12)..].Trim();
                var idxL = afterS.IndexOf("LINKS:", StringComparison.Ordinal);
                var suggJson = idxL >= 0 ? afterS[..idxL].Trim() : afterS.Trim();
                try { suggestions = JsonSerializer.Deserialize<List<string>>(suggJson) ?? new(); } catch { }

                if (idxL >= 0)
                {
                    var linksJson = afterS[(idxL + 6)..].Trim();
                    try { links = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(linksJson) ?? new(); } catch { }
                }
            }

            return (reply, suggestions, links);
        }

        // Страховка: авто-ссылки по ключевым словам, когда модель сама не вернула LINKS
        private static List<Dictionary<string, string>> AutoLinks(string combinedLower)
        {
            var links = new List<Dictionary<string, string>>();
            var addedUrls = new HashSet<string>();
            foreach (var kv in PageKeywords)
                if (kv.Key.Any(kw => combinedLower.Contains(kw)) && addedUrls.Add(kv.Value.url))
                {
                    links.Add(new() { ["text"] = kv.Value.name, ["url"] = kv.Value.url });
                    if (links.Count >= 2) break;
                }
            return links;
        }

        // Сохраняет пару сообщений (пациент + бот) в БД для аналитики админа.
        // Ошибка записи в лог никогда не должна ломать ответ пользователю —
        // поэтому все исключения гасятся здесь и только пишутся в лог сервера.
        private async Task LogExchangeAsync(ChatRequest req, string lang, string botReply)
        {
            try
            {
                var sessionId = string.IsNullOrWhiteSpace(req.SessionId)
                    ? Guid.NewGuid().ToString("N")
                    : req.SessionId!;
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var patientId = GetVerifiedPatientId();

                _db.ChatMessageLogs.AddRange(
                    new ChatMessageLog
                    {
                        SessionId = sessionId,
                        PatientId = patientId,
                        Role = "user",
                        Text = Truncate(req.Message, 1000),
                        Lang = lang,
                        ClientIp = ip
                    },
                    new ChatMessageLog
                    {
                        SessionId = sessionId,
                        PatientId = patientId,
                        Role = "bot",
                        Text = Truncate(botReply, 1000),
                        Lang = lang,
                        ClientIp = ip
                    });

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось сохранить лог чата — ответ пользователю уже отправлен, это не критично");
            }
        }

        private static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

        // БАГ (исправлено): раньше PatientId для лога чата брался прямо из тела
        // запроса (req.PatientId) без всякой проверки — точно так же, как раньше
        // было с записями на приём и отзывами до того, как их исправили (см.
        // комментарии в AppointmentRequestController/ReviewController: "PatientId
        // берём из токена, а не из тела запроса, иначе можно отправить от имени
        // чужого пациента"). Здесь эта же дыра оставалась: любой человек мог
        // подставить в JSON чужой patientId, и в админской аналитике диалог
        // выглядел бы так, будто его вёл другой, ни в чём не повинный пациент.
        // Эндпоинт чата специально доступен без авторизации (гости тоже пишут
        // боту), поэтому PatientId нельзя требовать — но можно и нужно доверять
        // ему, только если он подтверждён текущим JWT-токеном.
        private int? GetVerifiedPatientId()
        {
            if (User.Identity?.IsAuthenticated != true || !User.IsInRole("Patient"))
                return null;

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = "";
        public List<ChatMessage>? History { get; set; }
        public string Lang { get; set; } = "ru";

        // Идентификатор сессии чата в браузере (генерируется на фронте и
        // хранится в sessionStorage) — объединяет сообщения одного диалога
        // для админской аналитики.
        public string? SessionId { get; set; }

        // Раньше фронт передавал id авторизованного пациента здесь, и сервер ему
        // доверял напрямую — это позволяло подставить чужой id (см. фикс в
        // GetVerifiedPatientId). Поле оставлено для обратной совместимости
        // с фронтом, но сервер его больше не читает — вместо этого сам
        // проверяет личность по JWT-токену текущего запроса.
        public int? PatientId { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Text { get; set; } = "";
    }

    public class TtsRequest
    {
        public string Text { get; set; } = "";
    }
}