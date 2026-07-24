using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ChatKnowledgeService _knowledge;
    private readonly ILogger<DoctorController> _logger;

    public DoctorController(ApplicationDbContext db, ChatKnowledgeService knowledge, ILogger<DoctorController> logger)
    {
        _db = db;
        _knowledge = knowledge;
        _logger = logger;
    }

    // Публично: только активные врачи (для формы записи и т.п.)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var doctors = await _db.Doctors
            .Where(d => d.IsActive)
            .OrderBy(d => d.FullName)
            .ToListAsync();

        return Ok(doctors);
    }

    // Админ: все врачи, включая деактивированных — для управления списком
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAdmin()
    {
        var doctors = await _db.Doctors
            .OrderBy(d => d.FullName)
            .ToListAsync();

        return Ok(doctors);
    }

    // Админ: добавить нового врача
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateDoctorRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Укажите имя врача" });

        var doctor = new Doctor
        {
            FullName = req.FullName.Trim(),
            Specialization = req.Specialization?.Trim(),
            ExperienceYears = req.ExperienceYears,
            Bio = req.Bio?.Trim(),
            IsActive = true
        };

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        _knowledge.Invalidate();

        _logger.LogInformation("Добавлен новый врач: {FullName} (id={Id})", doctor.FullName, doctor.Id);

        return Ok(doctor);
    }

    // Админ: изменить имя врача и/или активность (деактивировать вместо удаления,
    // чтобы не потерять историю приёмов, где он указан как DoctorId)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDoctorRequest req)
    {
        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.FullName))
            doctor.FullName = req.FullName.Trim();

        if (req.IsActive.HasValue)
            doctor.IsActive = req.IsActive.Value;

        if (req.Specialization != null)
            doctor.Specialization = req.Specialization.Trim();

        if (req.ExperienceYears.HasValue)
            doctor.ExperienceYears = req.ExperienceYears;

        if (req.Bio != null)
            doctor.Bio = req.Bio.Trim();

        await _db.SaveChangesAsync();
        _knowledge.Invalidate();

        _logger.LogInformation("Обновлён врач id={Id}: {FullName}, активен={IsActive}", doctor.Id, doctor.FullName, doctor.IsActive);

        return Ok(doctor);
    }
}