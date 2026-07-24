namespace DentalClinic.Models;

public class UpdateDoctorRequest
{
    public string? FullName { get; set; }
    public bool? IsActive { get; set; }
    public string? Specialization { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
}