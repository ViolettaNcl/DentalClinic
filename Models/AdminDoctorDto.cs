namespace DentalClinic.Models;

/// <summary>
/// Admin editor projection. Excludes PhotoData so the doctors table does not
/// serialize multi-megabyte images on every dashboard refresh.
/// </summary>
public sealed record AdminDoctorDto(
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
    string? PhotoUrl,
    bool IsActive);
