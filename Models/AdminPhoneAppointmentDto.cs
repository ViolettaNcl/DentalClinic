namespace DentalClinic.Models
{
    public class AdminPhoneAppointmentDto
    {
        public string FirstName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime? AppointmentDate { get; set; }
        public string? Comment { get; set; }

        public int? DoctorId { get; set; }
    }
}