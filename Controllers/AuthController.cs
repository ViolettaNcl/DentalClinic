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
        private const string PasswordRequirementsMessage =
            "❌ Пароль должен содержать минимум 8 символов, включая заглавную и строчную буквы, цифру и специальный символ";

        private readonly ApplicationDbContext _db;
        private readonly JwtTokenService _tokens;
        private readonly ILogger<AuthController> _logger;
        private readonly NotificationService _notifications;
        private readonly TokenVersionCache? _tokenVersionCache;

        public AuthController(
            ApplicationDbContext db,
            JwtTokenService tokens,
            ILogger<AuthController> logger,
            NotificationService notifications,
            TokenVersionCache? tokenVersionCache = null)
        {
            _db = db;
            _tokens = tokens;
            _logger = logger;
            _notifications = notifications;
            _tokenVersionCache = tokenVersionCache;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest req,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Все поля обязательны" });

            if (!System.Text.RegularExpressions.Regex.IsMatch(req.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { message = "❌ Некорректный формат email" });

            if (!PasswordPolicy.IsValid(req.Password))
                return BadRequest(new { message = PasswordRequirementsMessage });

            var email = NormalizeEmail(req.Email);
            var patient = await IdentityEmailGuard.ExecuteSerializedAsync(_db, async () =>
            {
                if (await _db.Patients.AnyAsync(p => p.Email == email, cancellationToken)
                    || await _db.Admins.AnyAsync(a => a.Email == email, cancellationToken))
                    return null;

                var candidate = new Patient
                {
                    FirstName = req.FirstName.Trim(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
                };

                _db.Patients.Add(candidate);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    _db.Entry(candidate).State = EntityState.Detached;

                    // A uniqueness race is a controlled 409. Any other database
                    // failure must remain a real server error instead of being
                    // mislabeled as an account which already exists.
                    if (await EmailIsClaimedAsync(email, cancellationToken))
                        return null;

                    throw;
                }

                return candidate;
            }, cancellationToken);

            if (patient == null)
                return Conflict(new { message = "❌ Email уже зарегистрирован" });

            _logger.LogInformation("Зарегистрирован новый пациент id={Id}", patient.Id);

            // The patient row is already committed. A non-critical welcome
            // notification must not turn a successful registration into a 500 that
            // makes the client retry an account which now already exists.
            await _notifications.TryNotifyOptionalAsync(
                patient.Id,
                NotificationTypes.Welcome,
                $"Добро пожаловать, {patient.FirstName}! Спасибо за регистрацию 🦷",
                null,
                cancellationToken);

            _tokenVersionCache?.Set("Patient", patient.Id, patient.TokenVersion);
            IssueSessionCookie(_tokens.GenerateToken(
                patient.Id,
                patient.Email,
                patient.FirstName,
                "Patient",
                patient.TokenVersion,
                patient.AvatarUrl));

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
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest req,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = NormalizeEmail(req.Email);
            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

            if (patient == null || !BCrypt.Net.BCrypt.Verify(req.Password, patient.PasswordHash))
            {
                _logger.LogWarning("Неудачная попытка входа пациента");
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход пациента id={Id}", patient.Id);
            _tokenVersionCache?.Set("Patient", patient.Id, patient.TokenVersion);
            IssueSessionCookie(_tokens.GenerateToken(
                patient.Id,
                patient.Email,
                patient.FirstName,
                "Patient",
                patient.TokenVersion,
                patient.AvatarUrl));

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
        public async Task<IActionResult> AdminLogin(
            [FromBody] LoginRequest req,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "❌ Email и пароль обязательны" });

            var email = NormalizeEmail(req.Email);
            var admin = await _db.Admins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

            if (admin == null || !BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            {
                _logger.LogWarning("Неудачная попытка входа администратора");
                return Unauthorized(new { message = "❌ Email или пароль неверный" });
            }

            _logger.LogInformation("Вход администратора id={Id}", admin.Id);
            _tokenVersionCache?.Set("Admin", admin.Id, admin.TokenVersion);
            IssueSessionCookie(_tokens.GenerateToken(
                admin.Id,
                admin.Email,
                "Администратор",
                "Admin",
                admin.TokenVersion,
                admin.AvatarUrl));

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
            // Logout must never depend on a live round-trip to the remote Somee DB.
            // Expire the HttpOnly browser cookie immediately, then invalidate the
            // current token version in the in-process cache when its claims are present.
            DeleteSessionCookie();

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = User.FindFirstValue(ClaimTypes.Role);
                var versionText = User.FindFirstValue(JwtTokenService.TokenVersionClaim);

                if (int.TryParse(userIdText, out var userId)
                    && int.TryParse(versionText, out var tokenVersion)
                    && !string.IsNullOrWhiteSpace(role))
                {
                    _tokenVersionCache?.Set(role, userId, checked(tokenVersion + 1));
                }
            }

            return Ok(new { message = "Выход выполнен" });
        }

        [HttpGet("session")]
        [Authorize]
        public Task<IActionResult> GetSession(CancellationToken cancellationToken)
        {
            // Session bootstrap must stay independent of a second database round-trip.
            // The JWT has already been signature/lifetime/role validated by the auth
            // middleware (including TokenVersion when the database is reachable), so
            // the dashboard can become interactive even if the remote development SQL
            // server is temporarily slow during a pre-login handshake.
            _ = cancellationToken; // kept for endpoint cancellation contract; no database work is performed here.

            var userId = GetCurrentUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var name = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var avatarUrl = User.FindFirstValue(JwtTokenService.AvatarUrlClaim);

            if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                IActionResult result = Ok(new
                {
                    id = userId,
                    name,
                    email,
                    avatarUrl,
                    role = "patient"
                });
                return Task.FromResult(result);
            }

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                IActionResult result = Ok(new
                {
                    id = userId,
                    name = string.IsNullOrWhiteSpace(name) ? "Администратор" : name,
                    email,
                    avatarUrl,
                    role = "admin"
                });
                return Task.FromResult(result);
            }

            return Task.FromResult<IActionResult>(Forbid());
        }

        [HttpGet("profile")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            var patientId = GetCurrentUserId();
            var patient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
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
        public async Task<IActionResult> GetAdminProfile(CancellationToken cancellationToken)
        {
            var adminId = GetCurrentUserId();
            var admin = await _db.Admins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == adminId, cancellationToken);
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
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest req,
            CancellationToken cancellationToken)
        {
            var patient = await _db.Patients.FindAsync([GetCurrentUserId()], cancellationToken);
            if (patient == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.FirstName))
                patient.FirstName = req.FirstName.Trim();

            if (req.Phone != null)
                patient.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();

            await _db.SaveChangesAsync(cancellationToken);
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
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest req,
            CancellationToken cancellationToken)
        {
            var patient = await _db.Patients.FindAsync([GetCurrentUserId()], cancellationToken);
            if (patient == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, patient.PasswordHash))
                return BadRequest(new { message = "❌ Текущий пароль указан неверно" });

            if (!PasswordPolicy.IsValid(req.NewPassword))
                return BadRequest(new { message = PasswordRequirementsMessage });

            patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            patient.TokenVersion = checked(patient.TokenVersion + 1);
            await _db.SaveChangesAsync(cancellationToken);
            _tokenVersionCache?.Set("Patient", patient.Id, patient.TokenVersion);
            _logger.LogInformation("Пациент {Id} сменил пароль и отозвал прежние сессии", patient.Id);

            DeleteSessionCookie();

            return Ok(new { message = "✅ Пароль успешно изменён. Войдите снова." });
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private async Task<bool> EmailIsClaimedAsync(
            string email,
            CancellationToken cancellationToken)
            => await _db.Patients
                    .AsNoTracking()
                    .AnyAsync(p => p.Email == email, cancellationToken)
                || await _db.Admins
                    .AsNoTracking()
                    .AnyAsync(a => a.Email == email, cancellationToken);

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

        private void DeleteSessionCookie()
        {
            Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
