using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public class UpdateDoctorRequest
{
    [StringLength(150, MinimumLength = 1)]
    public string? FullName { get; set; }

    public bool? IsActive { get; set; }

    [StringLength(300)]
    public string? Specialization { get; set; }

    [Range(0, 80, ErrorMessage = "Стаж должен быть от 0 до 80 лет")]
    public int? ExperienceYears { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }
}