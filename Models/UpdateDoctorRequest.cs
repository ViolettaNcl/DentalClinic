using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public class UpdateDoctorRequest
{
    [StringLength(150, MinimumLength = 1)] public string? FullName { get; set; }
    [StringLength(150)] public string? FullNameEn { get; set; }
    [StringLength(150)] public string? FullNameFr { get; set; }
    [StringLength(150)] public string? FullNameEl { get; set; }
    [StringLength(150)] public string? FullNameAr { get; set; }

    public bool? IsActive { get; set; }

    [StringLength(300)] public string? Specialization { get; set; }

    [Range(0, 80, ErrorMessage = "Стаж должен быть от 0 до 80 лет")]
    public int? ExperienceYears { get; set; }

    public bool ClearExperienceYears { get; set; }

    [StringLength(500)] public string? Bio { get; set; }
    [StringLength(300)] public string? RoleTitle { get; set; }
    [StringLength(1200)] public string? Education { get; set; }
    [StringLength(1200)] public string? Skills { get; set; }
    [StringLength(500)] public string? Philosophy { get; set; }
    [StringLength(40)] public string? Stat2Value { get; set; }
    [StringLength(80)] public string? Stat2Label { get; set; }
    [StringLength(40)] public string? Stat3Value { get; set; }
    [StringLength(80)] public string? Stat3Label { get; set; }
}
