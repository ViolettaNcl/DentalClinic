using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace DentalClinic.Services
{
    // ═══════════════════════════════════════════════════════════════════
    //  "Лёгкий RAG" для AI-ассистента (Дента).
    //
    //  Раньше врачи и цены были зашиты прямо в C#-строку системного промпта
    //  ChatController'а — любое изменение прайса требовало правки кода и
    //  передеплоя. Теперь эти данные обычные строки в таблицах Services и
    //  Doctors, которые правит админ через панель, а этот сервис на лету
    //  собирает из них актуальный блок знаний для промпта.
    //
    //  Почему не полноценный векторный RAG с embeddings? Каталог клиники —
    //  это несколько десятков услуг и пара врачей, целиком помещающиеся в
    //  системный промпт. Прямой SELECT из БД даёт тот же результат (бот
    //  всегда видит актуальные данные без пересборки бэкенда), что и
    //  векторный поиск, но без инфраструктуры под embeddings и vector store.
    //  Если каталог вырастет на порядки (сотни услуг, база статей и т.п.) —
    //  сюда можно подключить embedding-поиск по полю Keywords/Description
    //  и отдавать в промпт только релевантные N позиций вместо всех.
    // ═══════════════════════════════════════════════════════════════════
    public class ChatKnowledgeService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;

        private const string CacheKey = "chat_knowledge_prompt_v1";

        // Короткий TTL — если админ забудет вручную дёрнуть Invalidate(),
        // бот всё равно подхватит изменения в течение пары минут.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        public ChatKnowledgeService(ApplicationDbContext db, IMemoryCache cache, IConfiguration config)
        {
            _db = db;
            _cache = cache;
            _config = config;
        }

        // Вызывается из ServiceController/DoctorController после любого
        // изменения цены или врача, чтобы бот увидел новые данные мгновенно,
        // а не ждал истечения кэша.
        public void Invalidate() => _cache.Remove(CacheKey);

        public async Task<string> GetKnowledgeBlockAsync()
        {
            if (_cache.TryGetValue(CacheKey, out string? cached) && cached != null)
                return cached;

            var doctors = await _db.Doctors
                .Where(d => d.IsActive)
                .OrderBy(d => d.FullName)
                .ToListAsync();

            var services = await _db.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
                .ToListAsync();

            var sb = new StringBuilder();

            sb.AppendLine("=== ВРАЧИ ===");
            if (doctors.Count == 0)
            {
                sb.AppendLine("Список врачей временно недоступен — направь пациента на /pages/doctors.html");
            }
            else
            {
                foreach (var d in doctors)
                {
                    sb.Append("Dr. ").Append(d.FullName);
                    if (!string.IsNullOrWhiteSpace(d.Specialization))
                        sb.Append(" (").Append(d.Specialization).Append(')');
                    if (d.ExperienceYears is > 0)
                        sb.Append(", стаж ").Append(d.ExperienceYears).Append("+ лет");
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== ЦЕНЫ ===");
            if (services.Count == 0)
            {
                sb.AppendLine("Прайс временно недоступен — направь пациента на /pages/services.html");
            }
            else
            {
                foreach (var group in services.GroupBy(s => s.Category))
                {
                    sb.Append(group.Key).Append(": ");
                    sb.Append(string.Join(", ", group.Select(FormatServiceLine)));
                    sb.AppendLine();
                }
            }

            var block = sb.ToString();
            _cache.Set(CacheKey, block, CacheTtl);
            return block;
        }

        private static string FormatServiceLine(Service s)
        {
            string price = (s.PriceTo.HasValue && s.PriceTo.Value != s.PriceFrom)
                ? $"{s.PriceFrom:N0}–{s.PriceTo:N0}₽"
                : $"{s.PriceFrom:N0}₽";
            string unit = string.IsNullOrWhiteSpace(s.Unit) ? "" : $"/{s.Unit}";
            string suffix = string.IsNullOrWhiteSpace(s.Description) ? "" : $" ({s.Description})";
            return $"{s.Name} {price}{unit}{suffix}";
        }

        // Контакты клиники берутся из appsettings.json (секция "Clinic"), а не
        // из БД — они меняются на порядки реже цен, а редактирование через
        // конфиг не требует пересборки кода (только перезапуск сервиса).
        public string GetContactsBlock()
        {
            var phone = _config["Clinic:Phone"] ?? "+7 (499) 999-99-99";
            var email = _config["Clinic:Email"] ?? "support@dentalclinic.ru";
            var address = _config["Clinic:Address"] ?? "Волгоград, ул. Мира 25";
            var hours = _config["Clinic:Hours"] ?? "Пн-Сб 9-20";
            return $"Контакты: {phone} | {email} | {address} | {hours}";
        }
    }
}