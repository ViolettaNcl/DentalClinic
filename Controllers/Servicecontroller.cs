using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

// Прайс клиники как обычные данные в БД, а не строка в системном промпте
// чат-бота. Публичный GET используется страницей /pages/services.html,
// админские методы — панелью администратора. Любое изменение сразу видно
// AI-ассистенту (Дента) — см. ChatKnowledgeService.Invalidate().
[ApiController]
[Route("api/[controller]")]
public class ServiceController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ChatKnowledgeService _knowledge;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(ApplicationDbContext db, ChatKnowledgeService knowledge, ILogger<ServiceController> logger)
    {
        _db = db;
        _knowledge = knowledge;
        _logger = logger;
    }

    // Публично: только активные услуги, сгруппированные по категориям
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var services = await _db.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync();

        return Ok(services);
    }

    // Админ: весь прайс, включая деактивированные позиции
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAdmin()
    {
        var services = await _db.Services
            .OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync();

        return Ok(services);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] Service req)
    {
        if (string.IsNullOrWhiteSpace(req.Category) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Укажите категорию и название услуги" });

        if (!ServiceCatalogPolicy.IsValidPriceRange(req.PriceFrom, req.PriceTo))
            return BadRequest(new { message = "Проверьте диапазон цен: значения не могут быть отрицательными, а цена 'до' не может быть ниже цены 'от'" });

        if (!ServiceCatalogPolicy.IsValidPageUrl(req.PageUrl))
            return BadRequest(new { message = "Ссылка услуги должна вести на локальную страницу /pages/..." });

        if (!ServiceCatalogPolicy.IsValidSortOrder(req.SortOrder))
            return BadRequest(new { message = "Порядок отображения не может быть отрицательным" });

        var pageUrl = req.PageUrl?.Trim();
        if (await HasActivePageSlotConflictAsync(pageUrl, req.SortOrder))
            return Conflict(new { message = "Этот порядок уже занят другой активной услугой на той же странице. Выберите другой номер или 0." });

        var service = new Service
        {
            Category = req.Category.Trim(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            PriceFrom = req.PriceFrom,
            PriceTo = req.PriceTo,
            Unit = req.Unit?.Trim(),
            Keywords = req.Keywords?.Trim(),
            PageUrl = pageUrl,
            SortOrder = req.SortOrder,
            IsActive = true
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        _knowledge.Invalidate();

        _logger.LogInformation("Добавлена услуга: {Category}/{Name} (id={Id})", service.Category, service.Name, service.Id);

        return Ok(service);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceRequest req)
    {
        var service = await _db.Services.FindAsync(id);
        if (service == null) return NotFound();

        // Рассчитываем будущие значения заранее, чтобы частичное обновление не
        // могло оставить некорректный диапазон или занять чужой live-card слот.
        var nextPriceFrom = req.PriceFrom ?? service.PriceFrom;
        var nextPriceTo = req.ClearPriceTo == true
            ? null
            : req.PriceTo ?? service.PriceTo;
        var nextPageUrl = req.PageUrl != null ? req.PageUrl.Trim() : service.PageUrl;
        var nextSortOrder = req.SortOrder ?? service.SortOrder;
        var nextIsActive = req.IsActive ?? service.IsActive;

        if (!ServiceCatalogPolicy.IsValidPriceRange(nextPriceFrom, nextPriceTo))
            return BadRequest(new { message = "Проверьте диапазон цен: значения не могут быть отрицательными, а цена 'до' не может быть ниже цены 'от'" });

        if (req.PageUrl != null && !ServiceCatalogPolicy.IsValidPageUrl(req.PageUrl))
            return BadRequest(new { message = "Ссылка услуги должна вести на локальную страницу /pages/..." });

        if (!ServiceCatalogPolicy.IsValidSortOrder(nextSortOrder))
            return BadRequest(new { message = "Порядок отображения не может быть отрицательным" });

        if (nextIsActive && await HasActivePageSlotConflictAsync(nextPageUrl, nextSortOrder, id))
            return Conflict(new { message = "Этот порядок уже занят другой активной услугой на той же странице. Выберите другой номер или 0." });

        if (!string.IsNullOrWhiteSpace(req.Category)) service.Category = req.Category.Trim();
        if (!string.IsNullOrWhiteSpace(req.Name)) service.Name = req.Name.Trim();
        if (req.Description != null) service.Description = req.Description.Trim();
        if (req.PriceFrom.HasValue) service.PriceFrom = req.PriceFrom.Value;
        if (req.ClearPriceTo == true) service.PriceTo = null;
        else if (req.PriceTo.HasValue) service.PriceTo = req.PriceTo.Value;
        if (req.Unit != null) service.Unit = req.Unit.Trim();
        if (req.Keywords != null) service.Keywords = req.Keywords.Trim();
        if (req.PageUrl != null) service.PageUrl = req.PageUrl.Trim();
        if (req.SortOrder.HasValue) service.SortOrder = req.SortOrder.Value;
        if (req.IsActive.HasValue) service.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync();
        _knowledge.Invalidate();

        _logger.LogInformation("Обновлена услуга id={Id}: {Category}/{Name}", service.Id, service.Category, service.Name);

        return Ok(service);
    }

    // Деактивация вместо удаления — как и у врачей, чтобы не терять историю
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var service = await _db.Services.FindAsync(id);
        if (service == null) return NotFound();

        service.IsActive = false;
        await _db.SaveChangesAsync();
        _knowledge.Invalidate();

        return Ok(service);
    }

    private async Task<bool> HasActivePageSlotConflictAsync(string? pageUrl, int sortOrder, int? excludeId = null)
    {
        if (sortOrder <= 0 || string.IsNullOrWhiteSpace(pageUrl)) return false;

        var normalizedPageUrl = pageUrl.Trim();
        var query = _db.Services
            .AsNoTracking()
            .Where(s => s.IsActive && s.PageUrl == normalizedPageUrl && s.SortOrder == sortOrder);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}
