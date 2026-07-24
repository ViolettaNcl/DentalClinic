using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("Reviews")]
public class Review
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(1000)]
    public string Text { get; set; } = null!;

    // pending | approved | rejected
    public string Status { get; set; } = "pending";

    // Причина отклонения, которую увидит пациент в личном кабинете
    [StringLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ModeratedAt { get; set; }

    // Пациент прочитал уведомление об отклонении (для "непрочитанного" бейджа в ЛК)
    public bool IsNotificationRead { get; set; } = false;
}
