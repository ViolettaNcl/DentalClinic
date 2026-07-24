using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

/// <summary>
/// Уведомление для пациента (колокольчик в личном кабинете).
/// Type: appointment_confirmed | appointment_cancelled | appointment_completed |
///       appointment_reminder | review_approved | review_rejected
/// </summary>
[Table("Notifications")]
public class Notification
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Required]
    [StringLength(40)]
    public string Type { get; set; } = null!;

    [Required]
    [StringLength(550)]
    public string Message { get; set; } = null!;

    // Ссылка на связанную запись (заявку на приём или отзыв) — необязательно
    public int? RelatedId { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}