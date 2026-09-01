using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

/// <summary>
/// Поля, которые посетителю действительно разрешено передать при создании заявки.
/// Статус, PatientId, ReminderSent и служебные даты назначаются только сервером.
/// </summary>
public sealed class CreateAppointmentRequest
{
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [StringLength(20)]
    [RegularExpression(@"^[\d\s\+\-\(\)]{5,20}$", ErrorMessage = "Некорректный формат телефона")]
    public string Phone { get; set; } = null!;

    public DateTime? AppointmentDate { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    public int? DoctorId { get; set; }
}
