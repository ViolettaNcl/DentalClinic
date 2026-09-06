namespace DentalClinic.Models;

/// <summary>
/// Public service catalogue shape. Retrieval keywords and activation flags are
/// operational/admin data and intentionally stay out of the unauthenticated API.
/// </summary>
public sealed record PublicServiceDto(
    int Id,
    string Category,
    string Name,
    string? Description,
    decimal PriceFrom,
    decimal? PriceTo,
    string? Unit,
    string? PageUrl,
    int SortOrder);
