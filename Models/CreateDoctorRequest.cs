namespace DentalClinic.Models;

public class CreateDoctorRequest
{
    public string FullName { get; set; } = null!;
    public string? Specialization { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
}