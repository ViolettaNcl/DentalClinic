using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("Doctors")]
public class Doctor
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(150)]
    public string? FullNameEn { get; set; }

    [StringLength(150)]
    public string? FullNameFr { get; set; }

    [StringLength(150)]
    public string? FullNameEl { get; set; }

    [StringLength(150)]
    public string? FullNameAr { get; set; }

    [StringLength(300)]
    public string? Specialization { get; set; }

    public int? ExperienceYears { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    // Rich public-card content. These fields let an administrator build the same
    // premium profile layout used by the curated doctors without editing HTML.
    [StringLength(300)]
    public string? RoleTitle { get; set; }

    [StringLength(1200)]
    public string? Education { get; set; }

    [StringLength(1200)]
    public string? Skills { get; set; }

    [StringLength(500)]
    public string? Philosophy { get; set; }

    [StringLength(40)]
    public string? Stat2Value { get; set; }

    [StringLength(80)]
    public string? Stat2Label { get; set; }

    [StringLength(40)]
    public string? Stat3Value { get; set; }

    [StringLength(80)]
    public string? Stat3Label { get; set; }

    [StringLength(350)]
    public string? PhotoUrl { get; set; }

    public byte[]? PhotoData { get; set; }

    [StringLength(50)]
    public string? PhotoContentType { get; set; }

    public bool IsActive { get; set; } = true;
}
