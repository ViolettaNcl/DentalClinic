using System.Globalization;

namespace DentalClinic.Services;

public sealed record PublicClinicProfile(
    string? Phone,
    string? Email,
    string? Address,
    string? Hours,
    double? Latitude,
    double? Longitude)
{
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    public static PublicClinicProfile FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var latitude = ParseCoordinate(configuration["Clinic:Latitude"], -90, 90);
        var longitude = ParseCoordinate(configuration["Clinic:Longitude"], -180, 180);

        // A half-configured coordinate pair is not actionable. Publish neither value
        // so the browser cannot accidentally build a route to an incomplete location.
        if (!latitude.HasValue || !longitude.HasValue)
        {
            latitude = null;
            longitude = null;
        }

        return new PublicClinicProfile(
            Clean(configuration["Clinic:Phone"]),
            Clean(configuration["Clinic:Email"]),
            Clean(configuration["Clinic:Address"]),
            Clean(configuration["Clinic:Hours"]),
            latitude,
            longitude);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static double? ParseCoordinate(string? value, double min, double max)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < min
            || parsed > max)
        {
            return null;
        }

        return parsed;
    }
}
