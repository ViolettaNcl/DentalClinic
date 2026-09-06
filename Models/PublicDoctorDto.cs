namespace DentalClinic.Models;

/// <summary>
/// Public projection for doctor catalogue consumers. Keeping this contract explicit
/// prevents future internal/admin-only Doctor fields from being exposed automatically.
/// </summary>
public sealed record PublicDoctorDto(
    int Id,
    string FullName,
    string? FullNameEn,
    string? FullNameFr,
    string? FullNameEl,
    string? FullNameAr,
    string? Specialization,
    int? ExperienceYears,
    string? Bio);
