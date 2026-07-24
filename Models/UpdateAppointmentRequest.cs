namespace DentalClinic.Models
{
    public class UpdateAppointmentRequest
    {
        public string? Status { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public string? Comment { get; set; }
        public int? DoctorId { get; set; }
    }
}