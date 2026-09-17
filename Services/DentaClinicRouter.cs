using System.Text;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Deterministic first-hop router for clinic facts. Questions that the application
/// can answer from SQL/configuration do not need a generative model. This keeps
/// doctors, prices, services, contacts and booking facts authoritative and usable
/// even when Gemini is temporarily unavailable.
/// </summary>
public sealed class DentaClinicRouter
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly DentaSiteKnowledgeService? _siteKnowledge;

    private static readonly string[] DoctorWords =
    {
        "врач", "доктор", "стоматолог", "специалист", "doctor", "dentist", "specialist",
        "médecin", "dentiste", "spécialiste", "γιατρ", "οδοντίατρ", "طبيب", "دكتور", "أخصائي"
    };

    private static readonly string[] ServiceWords =
    {
        "услуг", "лечени", "процедур", "services", "treatments", "service", "soins", "traitements",
        "υπηρεσ", "θεραπ", "خدمات", "علاج"
    };

    private static readonly string[] PriceWords =
    {
        "цен", "стоим", "сколько", "прайс", "price", "cost", "how much", "tarif", "prix", "coût",
        "τιμή", "κόστος", "πόσο", "سعر", "تكلفة", "كم"
    };

    private static readonly string[] SedationWords =
    {
        "седац", "под сном", "во сне", "sedation", "sedated", "sédation", "μέθη", "καταστολ", "تهدئة", "تخدير واع"
    };

    private static readonly string[] ContactWords =
    {
        "телефон", "номер", "адрес", "контакт", "график", "часы", "работаете", "открыт", "phone", "address",
        "contact", "hours", "open", "horaire", "adresse", "téléphone", "ωράριο", "διεύθυνση", "τηλέφων", "عنوان", "هاتف", "ساعات"
    };

    private static readonly string[] BookingIntentFragments =
    {
        "хочу запис", "записаться", "запишите", "запиши меня", "можно запис", "как запис", "запись на при",
        "хочу на приём", "хочу на прием",
        "book an appointment", "book appointment", "make an appointment", "schedule an appointment", "i want to book", "can i book",
        "prendre rendez-vous", "prendre rendez vous", "je veux prendre rendez", "réserver un rendez",
        "κλείσω ραντεβ", "κλείστε ραντεβ", "θέλω ραντεβ",
        "حجز موعد", "أريد حجز", "اريد حجز", "أحجز موعد", "احجز موعد"
    };

    private static readonly string[] FollowUpDoctorWords =
    {
        "из них", "кто из", "какой из", "какая из", "который из", "who of them", "which of them",
        "among them", "lequel", "laquelle", "parmi eux", "ποιος από", "ποια από", "من منهم", "أي منهم"
    };

    public DentaClinicRouter(ApplicationDbContext db, IConfiguration config)
        : this(db, config, siteKnowledge: null)
    {
    }

    public DentaClinicRouter(
        ApplicationDbContext db,
        IConfiguration config,
        DentaSiteKnowledgeService? siteKnowledge)
    {
        _db = db;
        _config = config;
        _siteKnowledge = siteKnowledge;
    }

    public async Task<DentaResponse?> TryAnswerAsync(
        string message,
        IReadOnlyList<DentaTurn> history,
        string language,
        CancellationToken cancellationToken)
    {
        var lang = NormalizeLanguage(language);
        var q = Normalize(message);

        var smallTalk = TrySmallTalk(q, lang);
        if (smallTalk is not null)
            return smallTalk;

        if (IsBookingIntent(q))
            return BookingAnswer(lang);

        if (ContainsAny(q, SedationWords))
            return await SedationAnswerAsync(lang, cancellationToken);

        var needsDoctorData = ContainsAny(q, DoctorWords) || ContainsAny(q, FollowUpDoctorWords);
        if (needsDoctorData)
        {
            var doctors = await _db.Doctors
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.FullName)
                .Take(30)
                .ToListAsync(cancellationToken);

            if (AsksChiefDoctor(q))
                return BuildChiefDoctorAnswer(doctors, lang);

            if (ContainsAny(q, DoctorWords) || IsDoctorFollowUp(q, history, doctors))
                return BuildDoctorsAnswer(q, doctors, lang);
        }

        var needsServiceData = ContainsAny(q, PriceWords)
                               || ContainsAny(q, ServiceWords)
                               || LooksLikeAllServices(q);
        if (needsServiceData)
        {
            var services = await _db.Services
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Take(100)
                .ToListAsync(cancellationToken);

            if (ContainsAny(q, PriceWords))
            {
                var matched = MatchServices(message, services);
                if (matched.Count > 0)
                    return BuildPriceAnswer(matched, lang);
            }

            if (ContainsAny(q, ServiceWords) || LooksLikeAllServices(q))
                return BuildServicesAnswer(services, lang);
        }

        if (ContainsAny(q, ContactWords))
            return BuildContactAnswer(q, lang);

        return null;
    }

    private async Task<DentaResponse> SedationAnswerAsync(string lang, CancellationToken cancellationToken)
    {
        var items = await _db.ClinicKnowledgeItems
            .AsNoTracking()
            .Where(k => k.IsActive)
            .OrderBy(k => k.SortOrder)
            .ThenBy(k => k.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        var sedation = items.FirstOrDefault(k =>
            ContainsAny(Normalize($"{k.Category} {k.Title} {k.Keywords}"), SedationWords));

        if (sedation is null)
        {
            return new DentaResponse
            {
                Source = "database",
                Reply = lang switch
                {
                    "en" => "I don't have a confirmed sedation policy in the clinic knowledge base yet. I can help you contact the clinic or book a consultation to clarify whether sedation is suitable for your visit.",
                    "fr" => "Je n’ai pas encore d’information confirmée sur la sédation dans la base de connaissances de la clinique. Je peux vous aider à contacter la clinique ou à prendre rendez-vous pour vérifier si elle convient à votre visite.",
                    "el" => "Δεν έχω ακόμη επιβεβαιωμένη πολιτική για καταστολή στη βάση γνώσεων της κλινικής. Μπορώ να σας βοηθήσω να επικοινωνήσετε με την κλινική ή να κλείσετε ραντεβού για διευκρίνιση.",
                    "ar" => "لا توجد لدي حالياً معلومات مؤكدة عن التهدئة في قاعدة معرفة العيادة. يمكنني مساعدتك في التواصل مع العيادة أو حجز موعد للتأكد من ملاءمتها لحالتك.",
                    _ => "В базе знаний клиники пока нет подтверждённой записи о седации. Могу помочь связаться с клиникой или записаться на консультацию, чтобы уточнить возможность седации для вашего визита."
                },
                Suggestions = CommonSuggestions(lang, includeBooking: true),
                Links = new() { Link(lang, "about", "/pages/about.html") }
            };
        }

        return new DentaResponse
        {
            Source = "database",
            Reply = T(lang,
                sedation.Content.Trim(),
                "Yes. The clinic knowledge base confirms that sedation is available for patients with dental anxiety, and the clinic site records an official sedation license. Suitability and the exact sedation plan are determined by the clinician after assessing the patient and contraindications.",
                "Oui. La base de connaissances de la clinique confirme que la sédation est proposée aux patients souffrant de phobie dentaire, et le site de la clinique mentionne une licence officielle pour la sédation. L’indication et le protocole sont déterminés par le médecin après évaluation du patient et des contre-indications.",
                "Ναι. Η βάση γνώσεων της κλινικής επιβεβαιώνει ότι παρέχεται καταστολή σε ασθενείς με οδοντιατρικό φόβο και ο ιστότοπος της κλινικής αναφέρει επίσημη άδεια για καταστολή. Η καταλληλότητα και το ακριβές πλάνο καθορίζονται από τον γιατρό μετά από αξιολόγηση και έλεγχο αντενδείξεων.",
                "نعم. تؤكد قاعدة معرفة العيادة توفر التهدئة للمرضى الذين يعانون من الخوف من علاج الأسنان، كما يذكر موقع العيادة وجود ترخيص رسمي لإجراء التهدئة. يحدد الطبيب مدى ملاءمتها والخطة المناسبة بعد تقييم الحالة وموانع الاستعمال."),
            Suggestions = CommonSuggestions(lang, includeBooking: true),
            Links = new()
            {
                Link(lang, "about", "/pages/about.html"),
                Link(lang, "contact", "/pages/contact.html")
            }
        };
    }

    private DentaResponse BuildChiefDoctorAnswer(IReadOnlyList<Doctor> doctors, string lang)
    {
        if (_siteKnowledge?.TryGetChiefDoctor(out var siteName, out var siteRole) == true)
        {
            return new DentaResponse
            {
                Source = "website",
                Reply = T(lang,
                    $"Главный врач клиники — **{siteName}**. На странице врачей он указан как **{siteRole}**.",
                    $"The clinic's chief doctor is **{siteName}**. On the doctors page, the role is listed as **{siteRole}**.",
                    $"Le médecin-chef de la clinique est **{siteName}**. Sur la page des médecins, son rôle est indiqué comme **{siteRole}**.",
                    $"Ο επικεφαλής ιατρός της κλινικής είναι ο/η **{siteName}**. Στη σελίδα των γιατρών αναφέρεται ως **{siteRole}**.",
                    $"الطبيب الرئيسي في العيادة هو **{siteName}**. وتعرض صفحة الأطباء صفته على أنها **{siteRole}**."),
                Suggestions = CommonSuggestions(lang, includeBooking: true),
                Links = new()
                {
                    Link(lang, "doctors", "/pages/doctors.html"),
                    Link(lang, "contact", "/pages/contact.html")
                }
            };
        }

        var activeNames = doctors.Take(10).Select(d => $"• **{DisplayDoctorName(d, lang)}**");
        var suffix = doctors.Count == 0 ? string.Empty : "\n" + string.Join("\n", activeNames);
        return new DentaResponse
        {
            Source = "database",
            Reply = T(lang,
                "В базе врачей нет отдельного подтверждённого поля «главный врач». Могу показать активных врачей и их специализации:",
                "The doctor database does not contain a separate confirmed 'chief doctor' field. I can show the active doctors and their specialties:",
                "La base des médecins ne contient pas de champ confirmé distinct « médecin-chef ». Je peux afficher les médecins actifs et leurs spécialités :",
                "Η βάση των γιατρών δεν περιέχει ξεχωριστό επιβεβαιωμένο πεδίο «επικεφαλής ιατρός». Μπορώ να δείξω τους ενεργούς γιατρούς και τις ειδικότητές τους:",
                "لا تحتوي قاعدة بيانات الأطباء على حقل مؤكد منفصل للطبيب الرئيسي. يمكنني عرض الأطباء النشطين وتخصصاتهم:") + suffix,
            Suggestions = CommonSuggestions(lang, includeBooking: true),
            Links = new() { Link(lang, "doctors", "/pages/doctors.html"), Link(lang, "contact", "/pages/contact.html") }
        };
    }

    private static DentaResponse? TrySmallTalk(string query, string lang)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        // Keep deterministic small-talk local so greetings and courtesies still work
        // even when the external AI provider is unavailable. Only short messages are
        // handled here; a greeting combined with a real clinic question continues to
        // the factual/AI pipeline.
        var tokenCount = Tokenize(query).Count;
        if (tokenCount > 8) return null;
        if (ContainsAny(query, DoctorWords)
            || ContainsAny(query, ServiceWords)
            || ContainsAny(query, PriceWords)
            || ContainsAny(query, SedationWords)
            || ContainsAny(query, ContactWords)
            || IsBookingIntent(query))
        {
            return null;
        }

        var isGreeting = query is "hi" or "hello" or "hey" or "bonjour" or "salut" or "γεια" or "مرحبا" or "أهلا"
                         || ContainsAny(query, new[]
                         {
                             "привет", "здравств", "добрый день", "добрый вечер", "доброе утро", "как дела", "как ваши дела",
                             "how are you", "comment ça va", "τι κάνεις", "السلام عليكم", "كيف حالك"
                         });

        if (isGreeting)
        {
            return new DentaResponse
            {
                Source = "router",
                Reply = T(lang,
                    "Привет! 👋 У Денты всё хорошо, спасибо. Я могу помочь с услугами и ценами, врачами, записью на приём или ответить на общий вопрос о стоматологии.",
                    "Hi! 👋 Denta is doing well, thank you. I can help with services and prices, doctors, appointments, or a general dental question.",
                    "Bonjour ! 👋 Denta va bien, merci. Je peux vous aider avec les services et tarifs, les médecins, les rendez-vous ou une question générale sur les soins dentaires.",
                    "Γεια σας! 👋 Η Denta είναι καλά, ευχαριστώ. Μπορώ να βοηθήσω με υπηρεσίες και τιμές, γιατρούς, ραντεβού ή μια γενική οδοντιατρική ερώτηση.",
                    "مرحباً! 👋 دنتا بخير، شكراً لك. يمكنني مساعدتك في الخدمات والأسعار والأطباء وحجز المواعيد أو الإجابة عن سؤال عام في طب الأسنان."),
                Suggestions = CommonSuggestions(lang, includeBooking: true)
            };
        }

        if (ContainsAny(query, new[] { "спасибо", "благодар", "thanks", "thank you", "merci", "ευχαριστ", "شكرا", "شكرًا" }))
        {
            return new DentaResponse
            {
                Source = "router",
                Reply = T(lang,
                    "Пожалуйста! 😊 Если понадобится, я помогу подобрать врача, посмотреть цены или записаться на приём.",
                    "You're welcome! 😊 If you need anything else, I can help you choose a doctor, check prices, or book an appointment.",
                    "Avec plaisir ! 😊 Je peux aussi vous aider à choisir un médecin, consulter les tarifs ou prendre rendez-vous.",
                    "Παρακαλώ! 😊 Μπορώ επίσης να βοηθήσω να επιλέξετε γιατρό, να δείτε τιμές ή να κλείσετε ραντεβού.",
                    "على الرحب والسعة! 😊 يمكنني أيضاً مساعدتك في اختيار الطبيب أو معرفة الأسعار أو حجز موعد."),
                Suggestions = CommonSuggestions(lang, includeBooking: true)
            };
        }

        return null;
    }

    private DentaResponse BuildDoctorsAnswer(string query, IReadOnlyList<Doctor> doctors, string lang)
    {
        if (doctors.Count == 0)
        {
            return new DentaResponse
            {
                Source = "database",
                Reply = T(lang,
                    "Сейчас в системе нет активных врачей. Актуальную информацию можно уточнить у администратора.",
                    "There are currently no active doctors in the system. Please check with the clinic administrator for the latest information.",
                    "Aucun médecin actif n’est actuellement enregistré dans le système. Veuillez vérifier les informations auprès de l’administrateur de la clinique.",
                    "Δεν υπάρχουν αυτή τη στιγμή ενεργοί γιατροί στο σύστημα. Επικοινωνήστε με τη γραμματεία για τις πιο πρόσφατες πληροφορίες.",
                    "لا يوجد حالياً أطباء نشطون مسجلون في النظام. يرجى التأكد من أحدث المعلومات لدى إدارة العيادة."),
                Links = new()
                {
                    Link(lang, "doctors", "/pages/doctors.html"),
                    Link(lang, "contact", "/pages/contact.html")
                }
            };
        }

        if (AsksMostExperienced(query))
        {
            var known = doctors.Where(d => d.ExperienceYears.HasValue).ToList();
            if (known.Count > 0)
            {
                var max = known.Max(d => d.ExperienceYears!.Value);
                var top = known.Where(d => d.ExperienceYears == max).ToList();
                var names = string.Join(", ", top.Select(d => DisplayDoctorName(d, lang)));
                return new DentaResponse
                {
                    Source = "database",
                    Reply = T(lang,
                        $"По данным системы, наибольший указанный опыт — {max} лет: **{names}**. Это сравнение только по полю стажа в базе, а не оценка качества врача.",
                        $"According to the clinic database, the highest recorded experience is {max} years: **{names}**. This compares only the recorded years of experience, not doctor quality.",
                        $"D’après la base de la clinique, l’expérience la plus élevée enregistrée est de {max} ans : **{names}**. Cette comparaison porte uniquement sur les années d’expérience enregistrées.",
                        $"Σύμφωνα με τη βάση της κλινικής, η μεγαλύτερη καταγεγραμμένη εμπειρία είναι {max} έτη: **{names}**. Η σύγκριση αφορά μόνο τα καταγεγραμμένα έτη εμπειρίας.",
                        $"وفقاً لقاعدة بيانات العيادة، أعلى خبرة مسجلة هي {max} سنوات: **{names}**. هذه مقارنة لسنوات الخبرة المسجلة فقط وليست تقييماً لجودة الطبيب."),
                    Suggestions = CommonSuggestions(lang, includeBooking: true),
                    Links = new() { Link(lang, "doctors", "/pages/doctors.html"), Link(lang, "contact", "/pages/contact.html") }
                };
            }
        }

        var specialtyTokens = ExtractSpecialtyTokens(query);
        var filtered = specialtyTokens.Count == 0
            ? doctors.ToList()
            : doctors.Where(d => DoctorMatchesSpecialty(d, specialtyTokens)).ToList();

        if (specialtyTokens.Count > 0 && filtered.Count == 0)
        {
            return new DentaResponse
            {
                Source = "database",
                Reply = T(lang,
                    "Среди активных врачей я не нашла подтверждённой специализации, совпадающей с вашим запросом. Могу показать всех врачей или помочь записаться, чтобы администратор подобрал специалиста.",
                    "I couldn't find a confirmed matching specialty among the active doctors. I can show all doctors or help you book so the clinic can choose the appropriate specialist.",
                    "Je n’ai pas trouvé de spécialité confirmée correspondant à votre demande parmi les médecins actifs. Je peux afficher tous les médecins ou vous aider à prendre rendez-vous.",
                    "Δεν βρήκα επιβεβαιωμένη ειδικότητα που να ταιριάζει στο αίτημά σας μεταξύ των ενεργών γιατρών. Μπορώ να εμφανίσω όλους τους γιατρούς ή να σας βοηθήσω να κλείσετε ραντεβού.",
                    "لم أجد تخصصاً مؤكداً يطابق طلبك بين الأطباء النشطين. يمكنني عرض جميع الأطباء أو مساعدتك في حجز موعد ليتم اختيار المختص المناسب."),
                Suggestions = CommonSuggestions(lang, includeBooking: true),
                Links = new() { Link(lang, "doctors", "/pages/doctors.html"), Link(lang, "contact", "/pages/contact.html") }
            };
        }

        var intro = specialtyTokens.Count > 0
            ? T(lang, "По данным базы клиники подходят:", "According to the clinic database, these doctors match:", "D’après la base de la clinique, ces médecins correspondent :", "Σύμφωνα με τη βάση της κλινικής, ταιριάζουν οι εξής γιατροί:", "وفقاً لقاعدة بيانات العيادة، هؤلاء الأطباء مناسبون:")
            : T(lang, "Сейчас в системе активны:", "The following doctors are currently active in the system:", "Les médecins suivants sont actuellement actifs dans le système :", "Οι παρακάτω γιατροί είναι αυτή τη στιγμή ενεργοί στο σύστημα:", "الأطباء النشطون حالياً في النظام هم:");

        var lines = filtered.Take(10).Select(d =>
        {
            var publicRole = d.Specialization;
            if (_siteKnowledge is not null
                && _siteKnowledge.TryGetDoctorRole(d.FullName, out var siteRole))
            {
                publicRole = siteRole;
            }
            var spec = string.IsNullOrWhiteSpace(publicRole) ? "" : $" — {publicRole}";
            var exp = d.ExperienceYears is > 0 ? ExperienceSuffix(d.ExperienceYears.Value, lang) : "";
            return $"• **{DisplayDoctorName(d, lang)}**{spec}{exp}";
        });

        return new DentaResponse
        {
            Source = "database",
            Reply = intro + "\n" + string.Join("\n", lines),
            Suggestions = CommonSuggestions(lang, includeBooking: true),
            Links = new() { Link(lang, "doctors", "/pages/doctors.html"), Link(lang, "contact", "/pages/contact.html") }
        };
    }

    private static DentaResponse BuildServicesAnswer(IReadOnlyList<Service> services, string lang)
    {
        if (services.Count == 0)
        {
            return new DentaResponse
            {
                Source = "database",
                Reply = T(lang,
                    "Список услуг сейчас недоступен в базе. Посмотрите страницу услуг или уточните у администратора.",
                    "The service list is currently unavailable in the database. Please check the services page or ask the clinic administrator.",
                    "La liste des services est actuellement indisponible dans la base. Consultez la page des services ou contactez l’administrateur.",
                    "Η λίστα υπηρεσιών δεν είναι διαθέσιμη αυτή τη στιγμή στη βάση. Δείτε τη σελίδα υπηρεσιών ή ρωτήστε τη γραμματεία.",
                    "قائمة الخدمات غير متاحة حالياً في قاعدة البيانات. يمكنك مراجعة صفحة الخدمات أو سؤال إدارة العيادة."),
                Links = new() { Link(lang, "services", "/pages/services.html") }
            };
        }

        var groups = services
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .Take(12)
            .Select(g =>
            {
                var items = string.Join("; ", g.Take(6).Select(s => $"{s.Name} — {FormatPrice(s, lang)}"));
                if (g.Count() > 6) items += "; …";
                return $"• **{g.Key}:** {items}";
            });

        return new DentaResponse
        {
            Source = "database",
            Reply = T(lang,
                "Вот активные услуги и цены из базы клиники:",
                "These are the active services and prices from the clinic database:",
                "Voici les services actifs et les tarifs enregistrés dans la base de la clinique :",
                "Αυτές είναι οι ενεργές υπηρεσίες και οι τιμές από τη βάση της κλινικής:",
                "هذه هي الخدمات والأسعار النشطة من قاعدة بيانات العيادة:") + "\n" + string.Join("\n", groups) + "\n" +
                T(lang,
                    "Итоговая стоимость уточняется после осмотра.",
                    "The final cost is confirmed after an examination.",
                    "Le coût final est confirmé après l’examen.",
                    "Το τελικό κόστος επιβεβαιώνεται μετά την εξέταση.",
                    "يتم تأكيد التكلفة النهائية بعد الفحص."),
            Suggestions = PriceSuggestions(lang),
            Links = new()
            {
                Link(lang, "services", "/pages/services.html"),
                Link(lang, "contact", "/pages/contact.html")
            }
        };
    }

    private static DentaResponse BuildPriceAnswer(IReadOnlyList<Service> services, string lang)
    {
        var lines = services.Take(8).Select(s =>
        {
            var price = FormatPrice(s, lang);
            var unit = string.IsNullOrWhiteSpace(s.Unit) ? "" : $" / {s.Unit}";
            return $"• **{s.Name}** — {price}{unit}";
        });

        var page = services.Select(s => s.PageUrl)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith("/pages/", StringComparison.Ordinal));

        var links = new List<DentaLink>();
        if (!string.IsNullOrWhiteSpace(page) && ServiceCatalogPolicy.IsValidPageUrl(page))
            links.Add(new DentaLink { Text = LocalizedDetailsLabel(lang), Url = page! });
        links.Add(Link(lang, "services", "/pages/services.html"));

        return new DentaResponse
        {
            Source = "database",
            Reply = T(lang,
                "По актуальному прайсу в базе:",
                "According to the current prices in the clinic database:",
                "Selon les tarifs actuels enregistrés dans la base :",
                "Σύμφωνα με τις τρέχουσες τιμές στη βάση της κλινικής:",
                "وفقاً للأسعار الحالية في قاعدة بيانات العيادة:") + "\n" + string.Join("\n", lines) + "\n" +
                T(lang,
                    "Итоговая стоимость зависит от осмотра и плана лечения.",
                    "The final cost depends on the examination and treatment plan.",
                    "Le coût final dépend de l’examen et du plan de traitement.",
                    "Το τελικό κόστος εξαρτάται από την εξέταση και το σχέδιο θεραπείας.",
                    "التكلفة النهائية تعتمد على الفحص وخطة العلاج."),
            Suggestions = CommonSuggestions(lang, includeBooking: true),
            Links = links.Take(2).ToList()
        };
    }

    private DentaResponse BuildContactAnswer(string query, string lang)
    {
        var phone = CleanConfig(_config["Clinic:Phone"]);
        var email = CleanConfig(_config["Clinic:Email"]);
        var address = CleanConfig(_config["Clinic:Address"]);
        var hours = CleanConfig(_config["Clinic:Hours"]);

        if (string.IsNullOrWhiteSpace(hours))
            hours = BuildHoursFromScheduling();

        var facts = new List<string>();
        if (!string.IsNullOrWhiteSpace(phone)) facts.Add(T(lang, $"Телефон: **{phone}**", $"Phone: **{phone}**", $"Téléphone : **{phone}**", $"Τηλέφωνο: **{phone}**", $"الهاتف: **{phone}**"));
        if (!string.IsNullOrWhiteSpace(email)) facts.Add(T(lang, $"Email: **{email}**", $"Email: **{email}**", $"E-mail : **{email}**", $"Email: **{email}**", $"البريد الإلكتروني: **{email}**"));
        if (!string.IsNullOrWhiteSpace(address)) facts.Add(T(lang, $"Адрес: **{address}**", $"Address: **{address}**", $"Adresse : **{address}**", $"Διεύθυνση: **{address}**", $"العنوان: **{address}**"));
        if (!string.IsNullOrWhiteSpace(hours)) facts.Add(T(lang, $"Часы работы: **{hours}**", $"Hours: **{hours}**", $"Horaires : **{hours}**", $"Ωράριο: **{hours}**", $"ساعات العمل: **{hours}**"));

        var reply = facts.Count == 0
            ? T(lang,
                "Контактные данные клиники сейчас не настроены. Я не буду придумывать номер или адрес — их нужно заполнить в конфигурации клиники.",
                "The clinic contact details are not configured right now. I won't invent a phone number or address; they need to be added to the clinic configuration.",
                "Les coordonnées de la clinique ne sont pas configurées actuellement. Je ne vais pas inventer de numéro ou d’adresse ; elles doivent être ajoutées à la configuration.",
                "Τα στοιχεία επικοινωνίας της κλινικής δεν έχουν ρυθμιστεί. Δεν θα επινοήσω τηλέφωνο ή διεύθυνση· πρέπει να προστεθούν στη ρύθμιση της κλινικής.",
                "بيانات التواصل مع العيادة غير مهيأة حالياً. لن أختلق رقم هاتف أو عنواناً؛ يجب إضافتها إلى إعدادات العيادة.")
            : string.Join("\n", facts);

        return new DentaResponse
        {
            Source = "configuration",
            Reply = reply,
            Suggestions = CommonSuggestions(lang, includeBooking: true),
            Links = new() { Link(lang, "contact", "/pages/contact.html") }
        };
    }

    private static DentaResponse BookingAnswer(string lang) => new()
    {
        Source = "router",
        Reply = T(lang,
            "Конечно. Помогу записаться на приём — нажмите кнопку ниже, и мы по шагам выберем данные для записи.",
            "Of course. I can help you book an appointment — use the button below and we'll go through the booking details step by step.",
            "Bien sûr. Je peux vous aider à prendre rendez-vous — utilisez le bouton ci-dessous et nous compléterons les informations étape par étape.",
            "Βεβαίως. Μπορώ να σας βοηθήσω να κλείσετε ραντεβού — πατήστε το κουμπί παρακάτω και θα συμπληρώσουμε τα στοιχεία βήμα-βήμα.",
            "بالتأكيد. يمكنني مساعدتك في حجز موعد — اضغط الزر أدناه وسنكمل بيانات الحجز خطوة بخطوة."),
        Suggestions = new() { BookingSuggestion(lang) },
        Links = new() { Link(lang, "contact", "/pages/contact.html") },
        StartBooking = true
    };

    private static List<Service> MatchServices(string rawQuery, IReadOnlyList<Service> services)
    {
        var queryTokens = Tokenize(rawQuery);
        if (queryTokens.Count == 0) return new();

        return services
            .Select(service => new
            {
                Service = service,
                Score = ScoreService(service, queryTokens)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Service.Category)
            .ThenBy(x => x.Service.SortOrder)
            .Take(8)
            .Select(x => x.Service)
            .ToList();
    }

    private static int ScoreService(Service service, IReadOnlySet<string> queryTokens)
    {
        var score = 0;
        score += ScoreText(service.Name, queryTokens, 8);
        score += ScoreText(service.Category, queryTokens, 6);
        score += ScoreText(service.Keywords, queryTokens, 10);
        score += ScoreText(service.Description, queryTokens, 2);
        return score;
    }

    private static int ScoreText(string? text, IReadOnlySet<string> queryTokens, int weight)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var fieldTokens = Tokenize(text);
        return queryTokens.Count(q => fieldTokens.Any(f => TokensMatch(q, f))) * weight;
    }

    private static bool DoctorMatchesSpecialty(Doctor doctor, IReadOnlySet<string> tokens)
    {
        var doctorTokens = Tokenize($"{doctor.Specialization} {doctor.Bio}");
        return tokens.Any(q => doctorTokens.Any(d => TokensMatch(q, d)));
    }

    private static HashSet<string> ExtractSpecialtyTokens(string query)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var normalized = query.ToLowerInvariant();

        if (ContainsAny(normalized, new[] { "implant", "имплант", "implantologie", "εμφύτευ", "زراع", "غرس" }))
            tokens.Add("имплант");
        if (ContainsAny(normalized, new[] { "хирург", "surgeon", "surgery", "chirurg", "χειρουργ", "جراح" }))
            tokens.Add("хирург");
        if (ContainsAny(normalized, new[] { "терап", "therap", "general dentist", "soins", "θεραπε", "علاج" }))
            tokens.Add("терап");
        if (ContainsAny(normalized, new[] { "эстет", "космет", "esthetic", "aesthetic", "cosmetic", "esthétique", "αισθητ", "تجميل" }))
            tokens.Add("эстет");
        if (ContainsAny(normalized, new[] { "эндодонт", "канал", "root canal", "endodont", "ρίζ", "قناة الجذر" }))
            tokens.Add("эндодонт");
        if (ContainsAny(normalized, new[] { "ортопед", "протез", "prosthodont", "prosthetic", "proth", "προσθετ", "تعويض" }))
            tokens.Add("протез");
        if (ContainsAny(normalized, new[] { "детск", "pediatric", "children", "enfant", "παιδ", "أطفال" }))
            tokens.Add("детск");
        if (ContainsAny(normalized, new[] { "ортодонт", "брекет", "orthodont", "brace", "orthodontie", "ορθοδοντ", "تقويم" }))
            tokens.Add("ортодонт");

        return tokens;
    }

    private static bool IsBookingIntent(string query) => ContainsAny(query, BookingIntentFragments);

    private static bool IsDoctorFollowUp(string query, IReadOnlyList<DentaTurn> history, IReadOnlyList<Doctor> doctors)
    {
        if (!ContainsAny(query, FollowUpDoctorWords) || history.Count == 0) return false;
        var recent = string.Join(" ", history.TakeLast(4).Select(h => h.Text));
        return doctors.Any(d => recent.Contains(d.FullName, StringComparison.OrdinalIgnoreCase))
               || ContainsAny(Normalize(recent), DoctorWords);
    }

    private static bool LooksLikeAllServices(string query) =>
        ContainsAny(query, new[] { "все услуг", "какие услуг", "что у вас есть", "all services", "what services", "tous les services", "όλες τις υπηρεσ", "جميع الخدمات" });

    private static bool AsksChiefDoctor(string query) => ContainsAny(query, new[]
    {
        "главный врач", "главный доктор", "кто главный", "chief doctor", "lead doctor", "head doctor",
        "médecin-chef", "médecin chef", "ιατρός επικεφαλής", "رئيس الأطباء", "الطبيب الرئيسي"
    });

    private static bool AsksMostExperienced(string query) => ContainsAny(query, new[]
    {
        "самый опыт", "больше опыта", "наибольший стаж", "most experienced", "highest experience", "plus expérimenté",
        "plus d'expérience", "πιο έμπειρ", "μεγαλύτερη εμπειρ", "الأكثر خبرة", "أعلى خبرة"
    });

    private string BuildHoursFromScheduling()
    {
        var mondayOpen = _config["Scheduling:WorkingHours:Monday:Open"];
        var mondayClose = _config["Scheduling:WorkingHours:Monday:Close"];
        var saturdayOpen = _config["Scheduling:WorkingHours:Saturday:Open"];
        var sundayClosed = _config.GetValue<bool?>("Scheduling:WorkingHours:Sunday:Closed") == true;

        if (string.IsNullOrWhiteSpace(mondayOpen) || string.IsNullOrWhiteSpace(mondayClose))
            return "";

        if (!string.IsNullOrWhiteSpace(saturdayOpen) && sundayClosed)
            return $"Пн–Сб {mondayOpen}–{mondayClose}; Вс — выходной";
        return $"{mondayOpen}–{mondayClose}";
    }

    private static string DisplayDoctorName(Doctor doctor, string lang) => lang switch
    {
        "en" when !string.IsNullOrWhiteSpace(doctor.FullNameEn) => doctor.FullNameEn!,
        "fr" when !string.IsNullOrWhiteSpace(doctor.FullNameFr) => doctor.FullNameFr!,
        "el" when !string.IsNullOrWhiteSpace(doctor.FullNameEl) => doctor.FullNameEl!,
        "ar" when !string.IsNullOrWhiteSpace(doctor.FullNameAr) => doctor.FullNameAr!,
        _ => doctor.FullName
    };

    private static string ExperienceSuffix(int years, string lang) => lang switch
    {
        "en" => $" ({years} years experience)",
        "fr" => $" ({years} ans d’expérience)",
        "el" => $" ({years} έτη εμπειρίας)",
        "ar" => $" (خبرة {years} سنوات)",
        _ => $" ({years} лет опыта)"
    };

    private static string FormatPrice(Service service, string lang)
    {
        static string Rub(decimal value) => $"{value:0.##}".Replace(".", ",", StringComparison.Ordinal) + " ₽";
        if (service.PriceTo.HasValue && service.PriceTo.Value > service.PriceFrom)
            return $"{Rub(service.PriceFrom)}–{Rub(service.PriceTo.Value)}";
        return lang == "ru" ? $"от {Rub(service.PriceFrom)}" : Rub(service.PriceFrom);
    }

    private static List<string> CommonSuggestions(string lang, bool includeBooking)
    {
        var result = lang switch
        {
            "en" => new List<string> { "Services and prices", "Our doctors" },
            "fr" => new List<string> { "Services et tarifs", "Nos médecins" },
            "el" => new List<string> { "Υπηρεσίες και τιμές", "Οι γιατροί μας" },
            "ar" => new List<string> { "الخدمات والأسعار", "أطباؤنا" },
            _ => new List<string> { "Услуги и цены", "Наши врачи" }
        };
        if (includeBooking) result.Add(BookingSuggestion(lang));
        return result.Take(3).ToList();
    }

    private static List<string> PriceSuggestions(string lang) => lang switch
    {
        "en" => new() { "Implant prices", "Crown prices", "Book an appointment" },
        "fr" => new() { "Prix des implants", "Prix des couronnes", "Prendre rendez-vous" },
        "el" => new() { "Τιμές εμφυτευμάτων", "Τιμές στεφανών", "Κλείστε ραντεβού" },
        "ar" => new() { "أسعار الزرعات", "أسعار التيجان", "احجز موعدًا" },
        _ => new() { "Цена имплантов", "Цена коронок", "Записаться на приём" }
    };

    private static string BookingSuggestion(string lang) => lang switch
    {
        "en" => "Book an appointment",
        "fr" => "Prendre rendez-vous",
        "el" => "Κλείστε ραντεβού",
        "ar" => "احجز موعدًا",
        _ => "Записаться на приём"
    };

    private static DentaLink Link(string lang, string kind, string url)
    {
        var text = kind switch
        {
            "doctors" => T(lang, "Открыть страницу врачей →", "Open doctors page →", "Ouvrir la page des médecins →", "Άνοιγμα σελίδας γιατρών →", "فتح صفحة الأطباء ←"),
            "services" => T(lang, "Открыть все услуги и цены →", "Open all services & prices →", "Ouvrir tous les services et tarifs →", "Άνοιγμα όλων των υπηρεσιών και τιμών →", "فتح جميع الخدمات والأسعار ←"),
            "about" => T(lang, "О клинике →", "About the clinic →", "À propos de la clinique →", "Σχετικά με την κλινική →", "عن العيادة ←"),
            _ => T(lang, "Контакты и запись →", "Contact & booking →", "Contact et rendez-vous →", "Επικοινωνία και ραντεβού →", "التواصل والحجز ←")
        };
        return new DentaLink { Text = text, Url = url };
    }

    private static string LocalizedDetailsLabel(string lang) => lang switch
    {
        "en" => "More details →",
        "fr" => "Plus de détails →",
        "el" => "Περισσότερες πληροφορίες →",
        "ar" => "مزيد من التفاصيل ←",
        _ => "Подробнее →"
    };

    private static string T(string lang, string ru, string en, string fr, string el, string ar) => lang switch
    {
        "en" => en,
        "fr" => fr,
        "el" => el,
        "ar" => ar,
        _ => ru
    };

    private static string NormalizeLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "en" => "en",
        "fr" => "fr",
        "el" => "el",
        "ar" => "ar",
        _ => "ru"
    };

    private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

    private static string CleanConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var v = value.Trim();
        return v.Contains("999-99-99", StringComparison.OrdinalIgnoreCase)
               || v.Contains("support@dentalclinic.ru", StringComparison.OrdinalIgnoreCase)
               || v.Contains("ул. Мира 25", StringComparison.OrdinalIgnoreCase)
            ? ""
            : v;
    }

    private static bool ContainsAny(string text, IEnumerable<string> fragments) =>
        fragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> Tokenize(string? value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return result;

        var current = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                current.Append(rune.ToString().ToLowerInvariant());
                continue;
            }
            FlushToken(current, result);
        }
        FlushToken(current, result);
        return result;
    }

    private static void FlushToken(StringBuilder current, ISet<string> result)
    {
        if (current.Length >= 3) result.Add(current.ToString());
        current.Clear();
    }

    private static bool TokensMatch(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal)) return true;
        if (left.Length < 4 || right.Length < 4) return false;
        var len = Math.Min(left.Length, right.Length);
        var common = 0;
        while (common < len && left[common] == right[common]) common++;
        return common >= 4;
    }
}
