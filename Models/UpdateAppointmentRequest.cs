using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class UpdateAppointmentRequest
    {
        [StringLength(20)]
        public string? Status { get; set; }

        public DateTime? AppointmentDate { get; set; }

        // Nullable value alone cannot distinguish "field omitted" from an
        // administrator intentionally clearing the scheduled date.
        public bool ClearAppointmentDate { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public int? DoctorId { get; set; }

        // Same explicit-clear contract as AppointmentDate: JSON null and an
        // omitted nullable integer look identical after normal model binding.
        public bool ClearDoctorId { get; set; }
    }
}
