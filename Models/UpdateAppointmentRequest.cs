using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class UpdateAppointmentRequest
    {
        [StringLength(20)]
        public string? Status { get; set; }

        public DateTime? AppointmentDate { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public int? DoctorId { get; set; }
    }
}
