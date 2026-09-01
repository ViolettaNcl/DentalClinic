using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("AppointmentRequests")]
public class AppointmentRequest
{
    [Key]
    public int Id { get; set; }

    public int? PatientId { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [StringLength(20)]
    [RegularExpression(@"^[\d\s\+\-\(\)]{5,20}$", ErrorMessage = "Некорректный формат телефона")]
    public string Phone { get; set; } = null!;

    public DateTime? AppointmentDate { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = AppointmentStatuses.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? DoctorId { get; set; }

    // Напоминание за день до приёма уже отправлено (чтобы фоновая служба не дублировала)
    public bool ReminderSent { get; set; } = false;
}
