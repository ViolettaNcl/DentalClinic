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
        private readonly DentaAssistantService _denta;

        // Админская аналитика должна оставаться предсказуемой по памяти даже если
        // чат накопит большой исторический объём. Точные totals/byDay считаются в БД,
        // а текстовая topic-аналитика работает на ограниченном недавнем срезе.
        private const int MaxStatsTopicMessages = 5000;
        private const int MaxRecentSessionMessages = 10000;

        // Fallback links are intentionally multilingual. Structured Gemini links are
        // preferred, but when the provider returns none this table must work for all
        // five supported UI languages rather than silently becoming Russian-only.
        private static readonly Dictionary<string[], (string url, Dictionary<string, string> names)> PageKeywords = new()
        {
            { new[]{"имплант","all-on","implant","implantologie","εμφύτευμ","زراع","غرس"}, ("/pages/services/implants.html", new()
                { ["ru"]="Подробнее об имплантах →", ["en"]="Implants →", ["fr"]="Implants →", ["el"]="Εμφυτεύματα →", ["ar"]="زراعة الأسنان ←" }) },
            { new[]{"коронк","crown","couronn","στεφάν","تاج","تيجان"}, ("/pages/services/crowns.html", new()
                { ["ru"]="Подробнее о коронках →", ["en"]="Crowns →", ["fr"]="Couronnes →", ["el"]="Στεφάνες →", ["ar"]="تيجان الأسنان ←" }) },
            { new[]{"пломб","filling","plombage","σφράγισ","حشو"}, ("/pages/services/fillings.html", new()
                { ["ru"]="Подробнее о пломбах →", ["en"]="Fillings →", ["fr"]="Plombages →", ["el"]="Σφραγίσματα →", ["ar"]="حشوات الأسنان ←" }) },
            { new[]{"канал","нерв","эндодонт","root canal","endodont","traitement de canal","ρίζ","ενδοδοντ","قناة الجذر","عصب"}, ("/pages/services/root-canal.html", new()
                { ["ru"]="Лечение каналов →", ["en"]="Root canal treatment →", ["fr"]="Traitement de canal →", ["el"]="Ενδοδοντική θεραπεία →", ["ar"]="علاج قناة الجذر ←" }) },
            { new[]{"удален","экстракц","зуб мудрост","extract","tooth removal","extraction","εξαγωγ","خلع"}, ("/pages/services/extractions.html", new()
                { ["ru"]="Удаление зубов →", ["en"]="Tooth extraction →", ["fr"]="Extraction dentaire →", ["el"]="Εξαγωγή δοντιού →", ["ar"]="خلع الأسنان ←" }) },
            { new[]{"мост","bridge","pont dentaire","γέφυρ","جسر"}, ("/pages/services/bridges.html", new()
                { ["ru"]="Подробнее о мостах →", ["en"]="Dental bridges →", ["fr"]="Bridges dentaires →", ["el"]="Οδοντικές γέφυρες →", ["ar"]="جسور الأسنان ←" }) },
            { new[]{"протез","denture","prosthet","prothès","οδοντοστοιχ","προσθετ","طقم","أطقم"}, ("/pages/services/prosthetics.html", new()
                { ["ru"]="Подробнее о протезах →", ["en"]="Dentures & prosthetics →", ["fr"]="Prothèses dentaires →", ["el"]="Οδοντικές προσθέσεις →", ["ar"]="أطقم وتعويضات الأسنان ←" }) },
            { new[]{"косметик","отбелив","винир","улыбк","cosmetic","whitening","veneer","smile design","esthétique","blanchiment","facette","sourire","αισθητικ","λεύκαν","όψ","تجميل","تبييض","قشور","ابتسامة"}, ("/pages/services/cosmetic-treatments.html", new()
                { ["ru"]="Косметические процедуры →", ["en"]="Cosmetic dentistry →", ["fr"]="Dentisterie esthétique →", ["el"]="Αισθητική οδοντιατρική →", ["ar"]="طب الأسنان التجميلي ←" }) },
            { new[]{"врач","доктор","специалист","doctor","dentist","specialist","médecin","dentiste","spécialiste","γιατρ","οδοντίατρ","طبيب","دكتور","أخصائي"}, ("/pages/doctors.html", new()
                { ["ru"]="Наши врачи →", ["en"]="Our doctors →", ["fr"]="Nos médecins →", ["el"]="Οι γιατροί μας →", ["ar"]="أطباؤنا ←" }) },
            { new[]{"записат","контакт","приём","book","appointment","contact","rendez-vous","rendez vous","ραντεβ","επικοινων","حجز","موعد","تواصل"}, ("/pages/contact.html", new()
                { ["ru"]="Записаться онлайн →", ["en"]="Book online →", ["fr"]="Prendre rendez-vous →", ["el"]="Κλείστε ραντεβού →", ["ar"]="احجز موعدًا ←" }) },
            { new[]{"о клинике","сертифик","философ","about clinic","certificate","philosophy","clinique","certificat","philosophie","κλινικ","πιστοποι","φιλοσοφ","عيادة","شهادة","فلسفة"}, ("/pages/about.html", new()
                { ["ru"]="О клинике →", ["en"]="About the clinic →", ["fr"]="À propos de la clinique →", ["el"]="Σχετικά με την κλινική →", ["ar"]="عن العيادة ←" }) },
        };

        // Проактивные сообщения — бот сам инициирует разговор.
        // Формулировки здесь уже безопасные сами по себе; runtime-фильтр остаётся
        // как защита для старых/кэшированных payload во время rolling deployments.
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
                ["ru"] = "Вижу, вы на странице о лечении каналов. Это может тревожить, но врач заранее обсудит обезболивание и способы сделать лечение максимально комфортным 🤍 Есть вопросы?",
                ["en"] = "I see you're on our root canal treatment page. It can sound worrying, but the dentist will discuss anesthesia and comfort options with you beforehand 🤍 Any questions?",
                ["fr"] = "Je vois que vous êtes sur notre page de traitement de canal. Cela peut sembler inquiétant, mais le dentiste vous expliquera à l’avance les options d’anesthésie et de confort 🤍 Des questions ?",
                ["el"] = "Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Μπορεί να ακούγεται ανησυχητικό, αλλά ο οδοντίατρος θα συζητήσει εκ των προτέρων τις επιλογές αναισθησίας και άνεσης 🤍 Έχετε ερωτήσεις;",
                ["ar"] = "أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مقلقًا، لكن طبيب الأسنان سيناقش معك مسبقًا خيارات التخدير والراحة 🤍 هل لديك أسئلة؟",
            },
            ["__proactive_extraction__"] = new()
            {
                ["ru"] = "Изучаете удаление зубов? Расскажу, чего ожидать от процедуры и как обычно контролируют боль и тревогу.",
                ["en"] = "Looking into tooth extraction? I can tell you what to expect and how pain and anxiety are typically managed.",
                ["fr"] = "Vous vous renseignez sur l'extraction dentaire ? Je peux vous expliquer à quoi vous attendre et comment la douleur et l’anxiété sont habituellement prises en charge.",
                ["el"] = "Εξετάζετε την εξαγωγή δοντιού; Μπορώ να σας πω τι να περιμένετε και πώς συνήθως αντιμετωπίζονται ο πόνος και το άγχος.",
                ["ar"] = "هل تبحث عن خلع الأسنان؟ يمكنني إخبارك بما يمكن توقعه وكيف تتم عادةً السيطرة على الألم والقلق.",
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
        private static string L(Dictionary<string, string> d, string lang) => d.TryGetValue(lang, out var v) ? v : d["ru"];
        private static string[] L(Dictionary<string, string[]> d, string lang) => d.TryGetValue(lang, out var v) ? v : d["ru"];

        public ChatController(
            IConfiguration config,
            IHttpClientFactory httpFactory,
            ILogger<ChatController> logger,
            ApplicationDbContext db,
            DentaAssistantService denta)
        {
            _config = config;
            _http = httpFactory.CreateClient();
            _logger = logger;
            _db = db;
            _denta = denta;
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
                return BadRequest(new { reply = L(ErrEmpty, lang), suggestions = Array.Empty<string>(), links = Array.Empty<object>(), startBooking = false });

            ClampLengths(req);

            // Proactive messages are deterministic and should not depend on the AI provider.
            if (ProactiveMessages.TryGetValue(req.Message, out var proMsgByLang))
            {
                var proReply = L(proMsgByLang, lang);
                return Ok(new
                {
                    reply = proReply,
                    suggestions = L(ProactiveSuggestions, lang),
                    links = AutoLinks(proReply, lang),
                    startBooking = false
                });
            }

            var result = await _denta.AnswerAsync(
                req.Message,
                ToDentaHistory(req.History),
                lang,
                HttpContext.RequestAborted);

            if (!result.Success || result.Response is null)
                return ProviderFailureResult(result, lang);

            var response = NormalizeResponse(result.Response, req.Message, lang);
            await LogExchangeAsync(req, lang, response.Reply);

            return Ok(new
            {
                reply = response.Reply,
                suggestions = response.Suggestions,
                links = response.Links,
                startBooking = response.StartBooking
            });
        }

        // The browser endpoint remains SSE-compatible, but Denta now validates one
        // complete typed response before anything is exposed. This avoids leaking
        // partial/raw structured JSON and removes the old marker parser entirely.
        [HttpPost("stream")]
        [EnableRateLimiting("chat")]
        public async Task ChatStream([FromBody] ChatRequest req)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var lang = NormalizeLang(req.Lang);

            async Task SendAsync(object payload)
            {
                // SSE bypasses MVC's normal JSON formatter, so serialize with the
                // ASP.NET web defaults explicitly. Without this, nested DTO fields
                // such as DentaLink.Text/Url are emitted as PascalCase while the
                // browser contract expects text/url, producing blank arrow buttons
                // that navigate to /undefined.
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }

            if (string.IsNullOrWhiteSpace(req.Message))
            {
                await SendAsync(new { error = L(ErrEmpty, lang), done = true });
                return;
            }

            ClampLengths(req);

            if (ProactiveMessages.TryGetValue(req.Message, out var proMsgByLang))
            {
                var proReply = L(proMsgByLang, lang);
                await SendAsync(new { delta = proReply });
                await SendAsync(new
                {
                    done = true,
                    suggestions = L(ProactiveSuggestions, lang),
                    links = AutoLinks(proReply, lang),
                    startBooking = false
                });
                return;
            }

            var result = await _denta.AnswerAsync(
                req.Message,
                ToDentaHistory(req.History),
                lang,
                HttpContext.RequestAborted);

            if (!result.Success || result.Response is null)
            {
                await SendAsync(new
                {
                    error = ProviderFailureMessage(result.Failure, lang),
                    done = true,
                    errorCode = result.Failure.ToString()
                });
                return;
            }

            var response = NormalizeResponse(result.Response, req.Message, lang);

            // One validated reply event, then one metadata event. The frontend still
            // uses the same SSE contract, but never sees provider JSON or legacy markers.
            await SendAsync(new { delta = response.Reply });
            await SendAsync(new
            {
                done = true,
                suggestions = response.Suggestions,
                links = response.Links,
                startBooking = response.StartBooking
            });

            await LogExchangeAsync(req, lang, response.Reply);
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
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подключиться к ElevenLabs TTS");
                Response.StatusCode = 502;
                return;
            }

            using var upstreamResponseLease = upstreamResp;

            if (!upstreamResp.IsSuccessStatusCode)
            {
                var errBody = await upstreamResp.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                _logger.LogWarning("Ошибка ElevenLabs TTS ({Status}): {Body}", (int)upstreamResp.StatusCode, errBody);
                Response.StatusCode = 502;
                return;
            }

            Response.ContentType = "audio/mpeg";
            await using var audioStream = await upstreamResp.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
            await audioStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
        }

        // ═══════════════════════════════════════════════════════════
        //  Админ: аналитика по чату — видно о чём спрашивают пациенты
        // ═══════════════════════════════════════════════════════════

        // Последние диалоги целиком, сгруппированные по сессии. Сессии выбираются
        // по последней активности, а не по времени первого сообщения: иначе старый
        // диалог, в который пациент вернулся сегодня, мог оказаться ниже более нового,
        // но уже неактивного диалога или вообще выпасть из take.
        [HttpGet("admin/sessions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRecentSessions(
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 200);

            var recentSessionIds = await _db.ChatMessageLogs
                .AsNoTracking()
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    SessionId = g.Key,
                    LastActivityAt = g.Max(m => m.CreatedAt)
                })
                .OrderByDescending(s => s.LastActivityAt)
                .ThenBy(s => s.SessionId)
                .Take(take)
                .Select(s => s.SessionId)
                .ToListAsync(cancellationToken);

            var logs = await _db.ChatMessageLogs
                .AsNoTracking()
                .Where(m => recentSessionIds.Contains(m.SessionId))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(MaxRecentSessionMessages)
                .ToListAsync(cancellationToken);

            if (logs.Count == MaxRecentSessionMessages)
                Response.Headers["X-Chat-Analytics-Truncated"] = "true";

            var sessions = logs
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    sessionId = g.Key,
                    startedAt = g.Min(m => m.CreatedAt),
                    lastActivityAt = g.Max(m => m.CreatedAt),
                    patientId = g.FirstOrDefault(m => m.PatientId != null)?.PatientId,
                    messageCount = g.Count(),
                    preview = g.FirstOrDefault(m => m.Role == "user")?.Text ?? g.First().Text,
                    messages = g
                        .OrderBy(m => m.CreatedAt)
                        .ThenBy(m => m.Id)
                        .Select(m => new { m.Role, m.Text, m.CreatedAt })
                })
                .OrderByDescending(s => s.lastActivityAt)
                .ThenBy(s => s.sessionId)
                .ToList();

            return Ok(sessions);
        }

        // Агрегированная статистика: totals и дневные значения считаются в БД,
        // а текстовый topic-анализ ограничен недавним срезом, чтобы один год
        // активного чата не загружал все сообщения в память веб-процесса.
        [HttpGet("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStats(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            days = Math.Clamp(days, 1, 365);
            var since = DateTime.UtcNow.AddDays(-days);

            var periodQuery = _db.ChatMessageLogs
                .AsNoTracking()
                .Where(m => m.CreatedAt >= since);
            var userQuery = periodQuery.Where(m => m.Role == "user");

            var totalMessages = await userQuery.CountAsync(cancellationToken);
            var totalSessions = await periodQuery
                .Select(m => m.SessionId)
                .Distinct()
                .CountAsync(cancellationToken);

            var byDayRows = await userQuery
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync(cancellationToken);

            var topicMessages = await userQuery
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Text)
                .Take(MaxStatsTopicMessages)
                .ToListAsync(cancellationToken);

            var topics = PageKeywords
                .Select(kv => new
                {
                    topic = kv.Value.names["ru"].Replace(" →", ""),
                    count = topicMessages.Count(text => kv.Key.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                })
                .Where(t => t.count > 0)
                .OrderByDescending(t => t.count)
                .ToList();

            var byDay = byDayRows
                .Select(g => new { date = g.Date.ToString("yyyy-MM-dd"), count = g.Count })
                .ToList();

            return Ok(new
            {
                totalMessages,
                totalSessions,
                topics,
                byDay,
                analyzedMessages = topicMessages.Count,
                topicsSampled = totalMessages > topicMessages.Count
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  Вспомогательные методы
        // ═══════════════════════════════════════════════════════════

        private static string NormalizeLang(string? lang) => string.IsNullOrWhiteSpace(lang) ? "ru" : lang.ToLowerInvariant();

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

        private static List<DentaTurn> ToDentaHistory(IEnumerable<ChatMessage>? history) =>
            history?
                .Where(m => !string.IsNullOrWhiteSpace(m.Text))
                .TakeLast(10)
                .Select(m => new DentaTurn(m.Role, m.Text.Trim()))
                .ToList()
            ?? new List<DentaTurn>();

        private static DentaResponse NormalizeResponse(DentaResponse response, string userMessage, string lang)
        {
            response.Reply = Truncate(response.Reply?.Trim(), 2200);
            response.Suggestions = (response.Suggestions ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Truncate(s.Trim(), 120))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            response.Links = (response.Links ?? new List<DentaLink>())
                .Where(link => !string.IsNullOrWhiteSpace(link.Text)
                               && DentaLinkPolicy.IsAllowedInternalUrl(link.Url)
                               && ServiceCatalogPolicy.IsValidPageUrl(link.Url))
                .GroupBy(link => link.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(2)
                .ToList();

            if (response.Links.Count == 0)
            {
                response.Links = AutoLinks(userMessage + " " + response.Reply, lang)
                    .Select(link => new DentaLink
                    {
                        Text = link["text"],
                        Url = link["url"]
                    })
                    .ToList();
            }

            return response;
        }

        private IActionResult ProviderFailureResult(DentaProviderResult result, string lang)
        {
            var status = result.Failure switch
            {
                DentaFailureKind.RateLimited => StatusCodes.Status429TooManyRequests,
                DentaFailureKind.Timeout => StatusCodes.Status504GatewayTimeout,
                DentaFailureKind.Network => StatusCodes.Status502BadGateway,
                DentaFailureKind.Authentication => StatusCodes.Status503ServiceUnavailable,
                DentaFailureKind.MissingApiKey => StatusCodes.Status503ServiceUnavailable,
                DentaFailureKind.ModelUnavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status502BadGateway
            };

            return StatusCode(status, new
            {
                reply = ProviderFailureMessage(result.Failure, lang),
                suggestions = Array.Empty<string>(),
                links = Array.Empty<object>(),
                startBooking = false,
                errorCode = result.Failure.ToString()
            });
        }

        private static string ProviderFailureMessage(DentaFailureKind failure, string lang)
        {
            static string Pick(string lang, string ru, string en, string fr, string el, string ar) => lang switch
            {
                "en" => en,
                "fr" => fr,
                "el" => el,
                "ar" => ar,
                _ => ru
            };

            return failure switch
            {
                DentaFailureKind.RateLimited => L(ErrOverloaded, lang),
                DentaFailureKind.Timeout => Pick(lang,
                    "AI отвечает слишком долго. Попробуйте ещё раз через несколько секунд.",
                    "The AI is taking too long to respond. Please try again in a few seconds.",
                    "L’IA met trop de temps à répondre. Réessayez dans quelques secondes.",
                    "Η AI αργεί να απαντήσει. Δοκιμάστε ξανά σε λίγα δευτερόλεπτα.",
                    "يستغرق المساعد وقتاً طويلاً للرد. حاول مرة أخرى بعد بضع ثوانٍ."),
                DentaFailureKind.Network => Pick(lang,
                    "Не удалось связаться с AI-сервисом. Проверьте соединение и попробуйте снова.",
                    "The clinic could not reach the AI service. Please check the connection and try again.",
                    "Impossible de joindre le service IA. Vérifiez la connexion et réessayez.",
                    "Δεν ήταν δυνατή η σύνδεση με την υπηρεσία AI. Ελέγξτε τη σύνδεση και δοκιμάστε ξανά.",
                    "تعذر الاتصال بخدمة الذكاء الاصطناعي. تحقق من الاتصال وحاول مرة أخرى."),
                DentaFailureKind.Authentication or DentaFailureKind.MissingApiKey => Pick(lang,
                    "AI-функция временно недоступна из-за настройки доступа. Вопросы о врачах, услугах, ценах и записи всё равно можно задать Денте.",
                    "The AI feature is temporarily unavailable because of its access configuration. Denta can still answer database-backed questions about doctors, services, prices and booking.",
                    "La fonction IA est temporairement indisponible en raison de sa configuration d’accès. Denta peut toujours répondre aux questions issues de la base sur les médecins, services, tarifs et rendez-vous.",
                    "Η λειτουργία AI είναι προσωρινά μη διαθέσιμη λόγω ρύθμισης πρόσβασης. Η Denta μπορεί ακόμη να απαντά σε ερωτήσεις από τη βάση για γιατρούς, υπηρεσίες, τιμές και ραντεβού.",
                    "ميزة الذكاء الاصطناعي غير متاحة مؤقتاً بسبب إعداد الوصول. ما زالت دنتا قادرة على الإجابة عن أسئلة قاعدة البيانات حول الأطباء والخدمات والأسعار والحجز."),
                DentaFailureKind.InvalidResponse => Pick(lang,
                    "Получен некорректный ответ AI. Попробуйте сформулировать вопрос ещё раз.",
                    "The AI returned an invalid response. Please try asking the question again.",
                    "L’IA a renvoyé une réponse invalide. Reformulez votre question et réessayez.",
                    "Η AI επέστρεψε μη έγκυρη απάντηση. Διατυπώστε ξανά την ερώτηση.",
                    "أعاد المساعد استجابة غير صالحة. أعد صياغة السؤال وحاول مرة أخرى."),
                DentaFailureKind.ModelUnavailable => Pick(lang,
                    "AI-модель временно недоступна. Попробуйте немного позже.",
                    "The AI model is temporarily unavailable. Please try again shortly.",
                    "Le modèle IA est temporairement indisponible. Réessayez un peu plus tard.",
                    "Το μοντέλο AI δεν είναι προσωρινά διαθέσιμο. Δοκιμάστε ξανά λίγο αργότερα.",
                    "نموذج الذكاء الاصطناعي غير متاح مؤقتاً. حاول مرة أخرى بعد قليل."),
                _ => Pick(lang,
                    "Сейчас не удалось получить ответ от AI-сервиса. Попробуйте ещё раз через несколько секунд; вопросы о врачах, услугах, ценах, сайте клиники и записи Дента продолжает обрабатывать напрямую.",
                    "The AI service could not answer right now. Please try again in a few seconds; Denta can still handle doctors, services, prices, clinic-site facts and booking directly.",
                    "Le service IA n’a pas pu répondre pour le moment. Réessayez dans quelques secondes ; Denta peut toujours traiter directement les médecins, services, tarifs, informations du site et rendez-vous.",
                    "Η υπηρεσία AI δεν μπόρεσε να απαντήσει αυτή τη στιγμή. Δοκιμάστε ξανά σε λίγα δευτερόλεπτα· η Denta εξακολουθεί να χειρίζεται απευθείας γιατρούς, υπηρεσίες, τιμές, στοιχεία του ιστότοπου και ραντεβού.",
                    "تعذر على خدمة الذكاء الاصطناعي الرد حالياً. حاول مرة أخرى بعد بضع ثوانٍ؛ وما زالت دنتا تتعامل مباشرة مع الأطباء والخدمات والأسعار ومعلومات موقع العيادة والحجز.")
            };
        }

        // Страховка: авто-ссылки по ключевым словам, когда модель сама не вернула LINKS.
        // Matching and labels both respect every supported language.
        private static List<Dictionary<string, string>> AutoLinks(string combinedText, string lang = "ru")
        {
            var links = new List<Dictionary<string, string>>();
            var addedUrls = new HashSet<string>();
            foreach (var kv in PageKeywords)
                if (kv.Key.Any(kw => combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase)) && addedUrls.Add(kv.Value.url))
                {
                    links.Add(new() { ["text"] = L(kv.Value.names, lang), ["url"] = kv.Value.url });
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
