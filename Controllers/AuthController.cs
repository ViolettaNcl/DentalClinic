using DentalClinic.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Services;
using System.Security.Claims;

namespace DentalClinic.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly JwtTokenService _tokens;
        private readonly ILogger<AuthController> _logger;
        private readonly NotificationService _notifications;

        public AuthController(ApplicationDbContext db, JwtTokenService tokens, ILogger<AuthController> logger, NotificationService notifications)
        {
            _db = db;
            _tokens = tokens;
            _logger = logger;
            _notifications = notifications;
        }

        // =========================
        // РЕГИСТРАЦИЯ ПАЦИЕНТА
        // =========================
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "❌ Все поля обязательны" });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return BadRequest(new { message = "❌ Некорректный формат email" });
            }

            if (req.Password.Length < 6)
            {
                return BadRequest(new { message = "❌ Пароль должен быть не короче 6 символов" });
            }

            var email = req.Email.Trim().ToLower();

            if (await _db.Patients.AnyAsync(p => p.Email.ToLower() == email))
            {
                return BadRequest(new { message = "❌ Email уже зарегистрирован" });
            }

            var patient = new Patient
            {
                FirstName = req.FirstName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Зарегистрирован новый пациент {Email} (id={Id})", patient.Email, patient.Id);

            // Приветственное уведомление — сразу ложится в БД и толкается через
            // SignalR. Если пользователь ещё не успел подключиться к хабу (токен
            // только что выдан в этом же ответе), пуш просто некому доставить —
            // уведомление всё равно будет в списке при следующей загрузке страницы.
            await _notifications.NotifyAsync(
                patient.Id,
                "welcome",
                $"Добро пожаловать, {patient.FirstName}! Спасибо за регистрацию 🦷",
                null);

            var token = _tokens.GenerateToken(patient.Id, patient.Email, patient.FirstName, "Patient");

            return Ok(new
            {
                message = "✅ Регистрация успешна!",
                id = patient.Id,
                name = patient.FirstName,
                email = patient.Email,
                avatarUrl = patient.AvatarUrl,
                role = "patient",
                token
            });
        }

        // =========================
        // ВХОД ПАЦИЕНТА
        // =========================
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            // req.Email/Password помечены как `required`, но это проверяется только
            // компилятором при создании объекта — явный null в JSON-теле запроса
            // всё равно проходит биндинг и раньше падал с 500 на .Trim(), вместо
            // аккуратного 400 клиенту.
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = req.Email.Trim().ToLower();

            var patient = await _db.Patients
                .FirstOrDefaultAsync(p => p.Email.ToLower() == email);

            if (patient == null ||
                !BCrypt.Net.BCrypt.Verify(req.Password, patient.PasswordHash))
            {
                _logger.LogWarning("Неудачная попытка входа пациента: {Email}", email);
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход пациента {Email} (id={Id})", patient.Email, patient.Id);

            var token = _tokens.GenerateToken(patient.Id, patient.Email, patient.FirstName, "Patient");

            return Ok(new
            {
                message = "✅ Вход успешен!",
                id = patient.Id,
                name = patient.FirstName,
                email = patient.Email,
                avatarUrl = patient.AvatarUrl,
                role = "patient",
                token
            });
        }

        // =========================
        // ВХОД АДМИНИСТРАТОРА
        // =========================
        [HttpPost("admin/login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = req.Email.Trim().ToLower();

            var admin = await _db.Admins
                .FirstOrDefaultAsync(a => a.Email.ToLower() == email);

            if (admin == null ||
                !BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            {
                _logger.LogWarning("Неудачная попытка входа администратора: {Email}", email);
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход администратора {Email} (id={Id})", admin.Email, admin.Id);

            var token = _tokens.GenerateToken(admin.Id, admin.Email, "Администратор", "Admin");

            return Ok(new
            {
                message = "✅ Вход администратора выполнен",
                id = admin.Id,
                name = "Администратор",
                email = admin.Email,
                avatarUrl = admin.AvatarUrl,
                role = "admin",
                token
            });
        }

        // =========================
        // ПРОФИЛЬ ПАЦИЕНТА: получить данные
        // =========================
        [HttpGet("profile")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetProfile()
        {
            var patient = await _db.Patients.FindAsync(GetCurrentUserId());
            if (patient == null) return NotFound();

            return Ok(new
            {
                id = patient.Id,
                firstName = patient.FirstName,
                email = patient.Email,
                phone = patient.Phone,
                avatarUrl = patient.AvatarUrl,
                createdAt = patient.CreatedAt
            });
        }

        // =========================
        // ПРОФИЛЬ АДМИНИСТРАТОРА: получить данные
        // =========================
        [HttpGet("admin/profile")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminProfile()
        {
            var admin = await _db.Admins.FindAsync(GetCurrentUserId());
            if (admin == null) return NotFound();

            return Ok(new
            {
                id = admin.Id,
                email = admin.Email,
                avatarUrl = admin.AvatarUrl,
                createdAt = admin.CreatedAt
            });
        }

        // =========================
        // ПРОФИЛЬ ПАЦИЕНТА: изменить имя и/или телефон
        // =========================
        [HttpPut("profile")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            var patient = await _db.Patients.FindAsync(GetCurrentUserId());
            if (patient == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.FirstName))
                patient.FirstName = req.FirstName.Trim();

            // Телефон можно и очистить, поэтому проверяем на null, а не на пустоту
            if (req.Phone != null)
                patient.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();

            await _db.SaveChangesAsync();

            _logger.LogInformation("Пациент {Id} обновил профиль", patient.Id);

            return Ok(new
            {
                message = "✅ Профиль обновлён",
                firstName = patient.FirstName,
                phone = patient.Phone
            });
        }

        // =========================
        // ПРОФИЛЬ ПАЦИЕНТА: смена пароля
        // =========================
        [HttpPut("change-password")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var patient = await _db.Patients.FindAsync(GetCurrentUserId());
            if (patient == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, patient.PasswordHash))
            {
                return BadRequest(new { message = "❌ Текущий пароль указан неверно" });
            }

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "❌ Новый пароль должен быть не короче 6 символов" });
            }

            patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Пациент {Id} сменил пароль", patient.Id);

            return Ok(new { message = "✅ Пароль успешно изменён" });
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}