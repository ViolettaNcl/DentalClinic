using DentalClinic.Models;

namespace DentalClinic.Services;

public enum DentaFailureKind
{
    None = 0,
    MissingApiKey,
    Authentication,
    RateLimited,
    ModelUnavailable,
    Timeout,
    Network,
    InvalidResponse,
    ProviderError
}

public sealed class DentaProviderResult
{
    public DentaResponse? Response { get; init; }
    public DentaFailureKind Failure { get; init; }
    public int? ProviderStatusCode { get; init; }
    public string? Model { get; init; }

    public bool Success => Response is not null && Failure == DentaFailureKind.None;

    public static DentaProviderResult Ok(DentaResponse response, string? model = null) => new()
    {
        Response = response,
        Failure = DentaFailureKind.None,
        Model = model
    };

    public static DentaProviderResult Failed(
        DentaFailureKind failure,
        int? providerStatusCode = null,
        string? model = null) => new()
    {
        Failure = failure,
        ProviderStatusCode = providerStatusCode,
        Model = model
    };
}
