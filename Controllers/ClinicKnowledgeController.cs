using System.ComponentModel.DataAnnotations;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/clinic-knowledge")]
[Authorize(Roles = "Admin")]
public class ClinicKnowledgeController : ControllerBase
{
    private static readonly HashSet<string> SupportedLocalizationLanguages =
        new(StringComparer.Ordinal) { "en", "fr", "el", "ar" };

    private readonly ApplicationDbContext _db;

    public ClinicKnowledgeController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.ClinicKnowledgeItems
            .AsNoTracking()
            .OrderByDescending(k => k.IsActive)
            .ThenBy(k => k.SortOrder)
            .ThenBy(k => k.Category)
            .ThenBy(k => k.Id)
            .Select(k => new
            {
                k.Id,
                k.Category,
                k.Title,
                k.Content,
                k.Keywords,
                k.SortOrder,
                k.IsActive,
                k.UpdatedAt,
                localizations = k.Localizations
                    .OrderBy(l => l.Lang)
                    .Select(l => new
                    {
                        l.Id,
                        l.Lang,
                        l.Title,
                        l.Content,
                        l.Keywords,
                        l.UpdatedAt
                    })
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ClinicKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasRequiredText(request))
            return BadRequest(new { message = "Заполните категорию, заголовок и содержание." });

        var localizationValidation = ValidateLocalizations(request.Localizations);
        if (localizationValidation != null)
            return BadRequest(new { message = localizationValidation });

        var now = DateTime.UtcNow;
        var item = new ClinicKnowledgeItem
        {
            Category = request.Category.Trim(),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Keywords = NormalizeOptional(request.Keywords),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            UpdatedAt = now
        };

        if (request.Localizations != null)
        {
            foreach (var localization in request.Localizations)
                item.Localizations.Add(CreateLocalization(localization, now));
        }

        _db.ClinicKnowledgeItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ClinicKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasRequiredText(request))
            return BadRequest(new { message = "Заполните категорию, заголовок и содержание." });

        var localizationValidation = ValidateLocalizations(request.Localizations);
        if (localizationValidation != null)
            return BadRequest(new { message = localizationValidation });

        var item = await _db.ClinicKnowledgeItems
            .Include(k => k.Localizations)
            .SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (item == null) return NotFound();

        var now = DateTime.UtcNow;
        item.Category = request.Category.Trim();
        item.Title = request.Title.Trim();
        item.Content = request.Content.Trim();
        item.Keywords = NormalizeOptional(request.Keywords);
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        item.UpdatedAt = now;

        // null = an older client did not send the localization field and therefore
        // must not accidentally erase translations. An explicit [] means clear all.
        if (request.Localizations != null)
            SynchronizeLocalizations(item, request.Localizations, now);

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClinicKnowledgeItems.FindAsync([id], cancellationToken);
        if (item == null) return NotFound();

        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(item);
    }

    private void SynchronizeLocalizations(
        ClinicKnowledgeItem item,
        IReadOnlyList<ClinicKnowledgeLocalizationWriteRequest> requested,
        DateTime now)
    {
        var requestedByLang = requested.ToDictionary(
            l => NormalizeLanguage(l.Lang),
            StringComparer.Ordinal);

        foreach (var existing in item.Localizations.ToList())
        {
            if (!requestedByLang.TryGetValue(existing.Lang, out var replacement))
            {
                _db.ClinicKnowledgeLocalizations.Remove(existing);
                continue;
            }

            existing.Title = replacement.Title.Trim();
            existing.Content = replacement.Content.Trim();
            existing.Keywords = NormalizeOptional(replacement.Keywords);
            existing.UpdatedAt = now;
            requestedByLang.Remove(existing.Lang);
        }

        foreach (var remaining in requestedByLang.Values)
            item.Localizations.Add(CreateLocalization(remaining, now));
    }

    private static ClinicKnowledgeLocalization CreateLocalization(
        ClinicKnowledgeLocalizationWriteRequest request,
        DateTime now)
        => new()
        {
            Lang = NormalizeLanguage(request.Lang),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Keywords = NormalizeOptional(request.Keywords),
            UpdatedAt = now
        };

    private static string? ValidateLocalizations(
        IReadOnlyList<ClinicKnowledgeLocalizationWriteRequest>? localizations)
    {
        if (localizations == null) return null;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var localization in localizations)
        {
            var lang = NormalizeLanguage(localization.Lang);
            if (!SupportedLocalizationLanguages.Contains(lang))
                return "Поддерживаются только проверенные переводы EN, FR, EL и AR.";
            if (!seen.Add(lang))
                return $"Для языка {lang.ToUpperInvariant()} можно сохранить только один перевод.";
            if (string.IsNullOrWhiteSpace(localization.Title)
                || string.IsNullOrWhiteSpace(localization.Content))
                return $"Для языка {lang.ToUpperInvariant()} заполните заголовок и содержание полностью.";
        }

        return null;
    }

    private static bool HasRequiredText(ClinicKnowledgeWriteRequest request)
        => !string.IsNullOrWhiteSpace(request.Category)
           && !string.IsNullOrWhiteSpace(request.Title)
           && !string.IsNullOrWhiteSpace(request.Content);

    private static string NormalizeLanguage(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object ToResponse(ClinicKnowledgeItem item)
        => new
        {
            item.Id,
            item.Category,
            item.Title,
            item.Content,
            item.Keywords,
            item.SortOrder,
            item.IsActive,
            item.UpdatedAt,
            localizations = item.Localizations
                .OrderBy(l => l.Lang)
                .Select(l => new
                {
                    l.Id,
                    l.Lang,
                    l.Title,
                    l.Content,
                    l.Keywords,
                    l.UpdatedAt
                })
        };
}

public sealed class ClinicKnowledgeWriteRequest
{
    [Required, StringLength(80)]
    public string Category { get; init; } = "";

    [Required, StringLength(160)]
    public string Title { get; init; } = "";

    [Required, StringLength(1200)]
    public string Content { get; init; } = "";

    [StringLength(300)]
    public string? Keywords { get; init; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyList<ClinicKnowledgeLocalizationWriteRequest>? Localizations { get; init; }
}

public sealed class ClinicKnowledgeLocalizationWriteRequest
{
    [Required, StringLength(2)]
    public string Lang { get; init; } = "";

    [Required, StringLength(160)]
    public string Title { get; init; } = "";

    [Required, StringLength(1200)]
    public string Content { get; init; } = "";

    [StringLength(300)]
    public string? Keywords { get; init; }
}
