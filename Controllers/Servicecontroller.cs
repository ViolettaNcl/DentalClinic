using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

// Прайс клиники как обычные данные в БД, а не строка в системном промпте
// чат-бота. Публичный GET используется страницей /pages/services.html,
// админские методы — панелью администратора. Дента читает ограниченный актуальный
// снимок из БД на каждый AI-запрос, поэтому изменения видны всем экземплярам сразу.
[ApiController]
[Route("api/[controller]")]
public class ServiceController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(ApplicationDbContext db, ILogger<ServiceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Публично: только активные услуги и только поля, необходимые каталогу/ценам.
    // Keywords и другие внутренние поля остаются доступны администратору и Денте,
    // но не публикуются автоматически в unauthenticated API.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var services = await _db.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new PublicServiceDto(
                s.Id,
                s.Category,
                s.Name,
                s.Description,
                s.PriceFrom,
                s.PriceTo,
                s.Unit,
                s.PageUrl,
                s.SortOrder))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    // Админ: весь прайс, включая деактивированные позиции
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAdmin(CancellationToken cancellationToken)
    {
        var services = await _db.Services
            .AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] Service req,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Category) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Укажите категорию и название услуги" });

        if (!ServiceCatalogPolicy.IsValidPriceRange(req.PriceFrom, req.PriceTo))
            return BadRequest(new { message = "Проверьте диапазон цен: от 0 до 99 999 999,99, не более 2 знаков после запятой; цена 'до' не может быть ниже цены 'от'" });

        if (!ServiceCatalogPolicy.IsValidPageUrl(req.PageUrl))
            return BadRequest(new { message = "Ссылка услуги должна вести на локальную страницу /pages/..." });

        if (!ServiceCatalogPolicy.IsValidSortOrder(req.SortOrder))
            return BadRequest(new { message = "Порядок отображения не может быть отрицательным" });

        var pageUrl = req.PageUrl?.Trim();
        if (await HasActivePageSlotConflictAsync(pageUrl, req.SortOrder, cancellationToken: cancellationToken))
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
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Добавлена услуга: {Category}/{Name} (id={Id})", service.Category, service.Name, service.Id);

        return Ok(service);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateServiceRequest req,
        CancellationToken cancellationToken)
    {
        var service = await _db.Services.FindAsync([id], cancellationToken);
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
            return BadRequest(new { message = "Проверьте диапазон цен: от 0 до 99 999 999,99, не более 2 знаков после запятой; цена 'до' не может быть ниже цены 'от'" });

        if (req.PageUrl != null && !ServiceCatalogPolicy.IsValidPageUrl(req.PageUrl))
            return BadRequest(new { message = "Ссылка услуги должна вести на локальную страницу /pages/..." });

        if (!ServiceCatalogPolicy.IsValidSortOrder(nextSortOrder))
            return BadRequest(new { message = "Порядок отображения не может быть отрицательным" });

        if (nextIsActive && await HasActivePageSlotConflictAsync(nextPageUrl, nextSortOrder, id, cancellationToken))
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

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Обновлена услуга id={Id}: {Category}/{Name}", service.Id, service.Category, service.Name);

        return Ok(service);
    }

    // Деактивация вместо удаления — как и у врачей, чтобы не терять историю
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var service = await _db.Services.FindAsync([id], cancellationToken);
        if (service == null) return NotFound();

        service.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(service);
    }

    private async Task<bool> HasActivePageSlotConflictAsync(
        string? pageUrl,
        int sortOrder,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (sortOrder <= 0 || string.IsNullOrWhiteSpace(pageUrl)) return false;

        var normalizedPageUrl = pageUrl.Trim();
        var query = _db.Services
            .AsNoTracking()
            .Where(s => s.IsActive && s.PageUrl == normalizedPageUrl && s.SortOrder == sortOrder);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }
}
