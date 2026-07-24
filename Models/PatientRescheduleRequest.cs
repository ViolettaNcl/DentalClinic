using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class PatientRescheduleRequest
    {
        [Required]
        public DateTime AppointmentDate { get; set; }
    }
}