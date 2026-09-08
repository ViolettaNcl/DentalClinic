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
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ClinicKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        var item = new ClinicKnowledgeItem
        {
            Category = request.Category.Trim(),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Keywords = NormalizeOptional(request.Keywords),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClinicKnowledgeItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ClinicKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _db.ClinicKnowledgeItems.FindAsync([id], cancellationToken);
        if (item == null) return NotFound();

        item.Category = request.Category.Trim();
        item.Title = request.Title.Trim();
        item.Content = request.Content.Trim();
        item.Keywords = NormalizeOptional(request.Keywords);
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(item);
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

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
}
