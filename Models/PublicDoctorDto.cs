namespace DentalClinic.Models;

/// <summary>
/// Explicit public projection for the doctor catalogue. Binary photo data and
/// future internal fields never leave the server through this contract.
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
    string? Bio,
    string? RoleTitle,
    string? Education,
    string? Skills,
    string? Philosophy,
    string? Stat2Value,
    string? Stat2Label,
    string? Stat3Value,
    string? Stat3Label,
    string? PhotoUrl);
