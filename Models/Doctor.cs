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

    // Имя врача на английском
    [StringLength(150)]
    public string? FullNameEn { get; set; }

    // Имя врача на французском
    [StringLength(150)]
    public string? FullNameFr { get; set; }

    // Имя врача на греческом
    [StringLength(150)]
    public string? FullNameEl { get; set; }

    // Имя врача на арабском
    [StringLength(150)]
    public string? FullNameAr { get; set; }

    // Специализация врача, например "импланты, хирургия" — используется
    // и на странице /pages/doctors.html, и AI-ассистентом (Дента) в ответах
    // пациентам, вместо того чтобы быть зашитой в промпт бота.
    [StringLength(300)]
    public string? Specialization { get; set; }

    public int? ExperienceYears { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;
}