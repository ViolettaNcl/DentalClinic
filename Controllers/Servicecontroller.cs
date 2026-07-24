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

        if (req.PriceFrom < 0)
            return BadRequest(new { message = "Цена не может быть отрицательной" });

        var service = new Service
        {
            Category = req.Category.Trim(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            PriceFrom = req.PriceFrom,
            PriceTo = req.PriceTo,
            Unit = req.Unit?.Trim(),
            Keywords = req.Keywords?.Trim(),
            PageUrl = req.PageUrl?.Trim(),
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

        // БАГ (исправлено): раньше сюда принималась целая сущность Service.
        // decimal PriceFrom не nullable — если клиент не передавал это поле
        // в JSON вообще, оно десериализовалось в 0, а проверка "req.PriceFrom >= 0"
        // пропускала 0 как валидное значение и молча обнуляла цену услуги
        // при любом частичном обновлении (например, если бы фронт присылал
        // только изменённое название). Теперь PriceFrom nullable в DTO,
        // и мы трогаем цену, только если она реально была передана.
        if (!string.IsNullOrWhiteSpace(req.Category)) service.Category = req.Category.Trim();
        if (!string.IsNullOrWhiteSpace(req.Name)) service.Name = req.Name.Trim();
        if (req.Description != null) service.Description = req.Description.Trim();
        if (req.PriceFrom.HasValue)
        {
            if (req.PriceFrom.Value < 0)
                return BadRequest(new { message = "Цена не может быть отрицательной" });
            service.PriceFrom = req.PriceFrom.Value;
        }
        if (req.PriceTo.HasValue) service.PriceTo = req.PriceTo;
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
}