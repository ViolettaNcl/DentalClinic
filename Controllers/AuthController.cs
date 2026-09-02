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
        private const string AuthCookieName = "dc_auth";

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

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Все поля обязательны" });

            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { message = "❌ Некорректный формат email" });

            if (req.Password.Length < 6)
                return BadRequest(new { message = "❌ Пароль должен быть не короче 6 символов" });

            var email = NormalizeEmail(req.Email);

            if (await _db.Patients.AnyAsync(p => p.Email == email))
                return BadRequest(new { message = "❌ Email уже зарегистрирован" });

            var patient = new Patient
            {
                FirstName = req.FirstName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            _db.Patients.Add(patient);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The unique DB index is the final guard against two simultaneous
                // registrations passing the AnyAsync check at the same time.
                return Conflict(new { message = "❌ Email уже зарегистрирован" });
            }

            _logger.LogInformation("Зарегистрирован новый пациент id={Id}", patient.Id);

            await _notifications.NotifyAsync(
                patient.Id,
                "welcome",
                $"Добро пожаловать, {patient.FirstName}! Спасибо за регистрацию 🦷",
                null);

            IssueSessionCookie(_tokens.GenerateToken(patient.Id, patient.Email, patient.FirstName, "Patient"));

            return Ok(new
            {
                message = "✅ Регистрация успешна!",
                id = patient.Id,
                name = patient.FirstName,
                email = patient.Email,
                avatarUrl = patient.AvatarUrl,
                role = "patient",
                expiresAt = _tokens.GetExpiryUtc()
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = NormalizeEmail(req.Email);
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Email == email);

            if (patient == null || !BCrypt.Net.BCrypt.Verify(req.Password, patient.PasswordHash))
            {
                // Never log the submitted email/phone/password. Authentication logs keep
                // only coarse event information so production logs do not become a PII store.
                _logger.LogWarning("Неудачная попытка входа пациента");
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход пациента id={Id}", patient.Id);
            IssueSessionCookie(_tokens.GenerateToken(patient.Id, patient.Email, patient.FirstName, "Patient"));

            return Ok(new
            {
                message = "✅ Вход успешен!",
                id = patient.Id,
                name = patient.FirstName,
                email = patient.Email,
                avatarUrl = patient.AvatarUrl,
                role = "patient",
                expiresAt = _tokens.GetExpiryUtc()
            });
        }

        [HttpPost("admin/login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = NormalizeEmail(req.Email);
            var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == email);

            if (admin == null || !BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            {
                _logger.LogWarning("Неудачная попытка входа администратора");
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход администратора id={Id}", admin.Id);
            IssueSessionCookie(_tokens.GenerateToken(admin.Id, admin.Email, "Администратор", "Admin"));

            return Ok(new
            {
                message = "✅ Вход администратора выполнен",
                id = admin.Id,
                name = "Администратор",
                email = admin.Email,
                avatarUrl = admin.AvatarUrl,
                role = "admin",
                expiresAt = _tokens.GetExpiryUtc()
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
            return Ok(new { message = "Выход выполнен" });
        }

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

        [HttpPut("profile")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            var patient = await _db.Patients.FindAsync(GetCurrentUserId());
            if (patient == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.FirstName))
                patient.FirstName = req.FirstName.Trim();

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

        [HttpPut("change-password")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var patient = await _db.Patients.FindAsync(GetCurrentUserId());
            if (patient == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, patient.PasswordHash))
                return BadRequest(new { message = "❌ Текущий пароль указан неверно" });

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return BadRequest(new { message = "❌ Новый пароль должен быть не короче 6 символов" });

            patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Пациент {Id} сменил пароль", patient.Id);

            // Invalidate the browser's old JWT immediately. The user signs in again and
            // receives a fresh token after a password change.
            Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            return Ok(new { message = "✅ Пароль успешно изменён. Войдите снова." });
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private void IssueSessionCookie(string token)
        {
            Response.Cookies.Append(AuthCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true,
                Expires = new DateTimeOffset(_tokens.GetExpiryUtc())
            });
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
