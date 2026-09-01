using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class AdminPhoneAppointmentDto
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [StringLength(20)]
        [RegularExpression(@"^[\d\s\+\-\(\)]{5,20}$", ErrorMessage = "Некорректный формат телефона")]
        public string Phone { get; set; } = null!;

        public DateTime? AppointmentDate { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public int? DoctorId { get; set; }
    }
}
