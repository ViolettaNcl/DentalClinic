using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

// Лог сообщений AI-чата (Дента) — сохраняется в БД, чтобы админ мог
// посмотреть, о чём спрашивают пациенты (раздел "Аналитика" → "AI-чат"),
// и понять какие услуги продвигать. Раньше история чата жила только в
// браузере пациента и терялась при закрытии вкладки.
[Table("ChatMessageLogs")]
public class ChatMessageLog
{
    [Key]
    public int Id { get; set; }

    // Все сообщения одного диалога (в рамках одной открытой сессии чата на
    // сайте) объединяются одним SessionId, чтобы админ видел переписку целиком
    [Required, StringLength(64)]
    public string SessionId { get; set; } = null!;

    // Если пациент авторизован — привязываем лог к нему (необязательно)
    public int? PatientId { get; set; }

    // "user" — сообщение пациента, "bot" — ответ Денты
    [Required, StringLength(10)]
    public string Role { get; set; } = "user";

    [Required, StringLength(1000)]
    public string Text { get; set; } = "";

    [StringLength(5)]
    public string Lang { get; set; } = "ru";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // IP используется только для защиты от накрутки статистики ботами,
    // в интерфейсе админа не показывается
    [StringLength(64)]
    public string? ClientIp { get; set; }
}