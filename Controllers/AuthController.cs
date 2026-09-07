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
        public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Все поля обязательны" });

            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { message = "❌ Некорректный формат email" });

            if (!PasswordPolicy.IsValid(req.Password))
                return BadRequest(new { message = PasswordPolicy.ErrorMessage() });

            var email = NormalizeEmail(req.Email);
            if (await _db.Patients.AnyAsync(p => p.Email == email, cancellationToken))
                return BadRequest(new { message = "❌ Email уже зарегистрирован" });

            var patient = new Patient
            {
                FirstName = req.FirstName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            _db.Patients.Add(patient);
            try { await _db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { return Conflict(new { message = "❌ Email уже зарегистрирован" }); }

            await _notifications.NotifyAsync(patient.Id, "welcome", $"Добро пожаловать, {patient.FirstName}! Спасибо за регистрацию 🦷", null, cancellationToken);
            IssueSessionCookie(_tokens.GenerateToken(patient.Id, patient.Email, patient.FirstName, "Patient", patient.TokenVersion));

            return Ok(new { message = "✅ Регистрация успешна!", id = patient.Id, name = patient.FirstName, email = patient.Email, avatarUrl = patient.AvatarUrl, role = "patient", expiresAt = _tokens.GetExpiryUtc() });
        }

        [HttpPut("change-password")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken cancellationToken)
        {
            var patient = await _db.Patients.FindAsync([GetCurrentUserId()], cancellationToken);
            if (patient == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, patient.PasswordHash))
                return BadRequest(new { message = "❌ Текущий пароль указан неверно" });

            if (!PasswordPolicy.IsValid(req.NewPassword))
                return BadRequest(new { message = PasswordPolicy.ErrorMessage() });

            patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            patient.TokenVersion = checked(patient.TokenVersion + 1);
            await _db.SaveChangesAsync(cancellationToken);
            DeleteSessionCookie();

            return Ok(new { message = "✅ Пароль успешно изменён. Войдите снова." });
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
        private void IssueSessionCookie(string token) => Response.Cookies.Append(AuthCookieName, token, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true, Expires = new DateTimeOffset(_tokens.GetExpiryUtc()) });
        private void DeleteSessionCookie() => Response.Cookies.Delete(AuthCookieName, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" });
        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
