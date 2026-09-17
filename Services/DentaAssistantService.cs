using DentalClinic.Models;

namespace DentalClinic.Services;

/// <summary>
/// Orchestrates Denta: authoritative SQL/config answers first, Gemini only when
/// natural-language reasoning or a general dental explanation is actually needed.
/// </summary>
public sealed class DentaAssistantService
{
    private readonly DentaClinicRouter _router;
    private readonly DentaAiService _ai;

    public DentaAssistantService(DentaClinicRouter router, DentaAiService ai)
    {
        _router = router;
        _ai = ai;
    }

    public async Task<DentaProviderResult> AnswerAsync(
        string message,
        IReadOnlyList<DentaTurn> history,
        string language,
        CancellationToken cancellationToken)
    {
        var direct = await _router.TryAnswerAsync(
            message,
            history,
            language,
            cancellationToken);

        if (direct is not null)
            return DentaProviderResult.Ok(direct, model: null);

        return await _ai.GenerateAsync(
            message,
            history,
            language,
            cancellationToken);
    }
}
