using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DentalClinic.Controllers
{
    // Загрузка и удаление аватара — общий эндпоинт и для админа, и для пациента:
    // роль читаем из JWT/cookie-сессии и обновляем нужную таблицу (Admins или Patients).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AvatarController : ControllerBase
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSizeBytes = 3 * 1024 * 1024; // 3 МБ
        private const string LegacyAvatarUrlPrefix = "/uploads/avatars/";
        private const string DurableAvatarUrl = "/api/avatar/content";

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AvatarController> _logger;

        public AvatarController(ApplicationDbContext db, IWebHostEnvironment env, ILogger<AvatarController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        // =========================
        // ЗАГРУЗКА / ЗАМЕНА АВАТАРА
        // =========================
        [HttpPost]
        [RequestSizeLimit(MaxFileSizeBytes + 1024)]
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "❌ Файл не передан" });

            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new { message = "❌ Файл слишком большой (максимум 3 МБ)" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { message = "❌ Разрешены только изображения JPG, PNG или WEBP" });

            // Расширение файла контролируется пользователем, поэтому само по себе
            // не доказывает, что это изображение. Проверяем сигнатуру содержимого
            // до сохранения в постоянное хранилище.
            await using (var validationStream = file.OpenReadStream())
            {
                if (!await ImageUploadValidator.MatchesExtensionAsync(
                        validationStream,
                        ext,
                        HttpContext.RequestAborted))
                {
                    return BadRequest(new { message = "❌ Содержимое файла не соответствует формату изображения" });
                }
            }

            var (role, id) = GetCurrentUser();
            if (id == null) return Unauthorized();
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            await using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, HttpContext.RequestAborted);
            var bytes = buffer.ToArray();
            var contentType = ContentTypeForExtension(ext);
            var avatarUrl = $"{DurableAvatarUrl}?v={Guid.NewGuid():N}";

            string? oldAvatarUrl;
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var admin = await _db.Admins.FindAsync([id.Value], HttpContext.RequestAborted);
                if (admin == null) return NotFound();

                oldAvatarUrl = admin.AvatarUrl;
                admin.AvatarData = bytes;
                admin.AvatarContentType = contentType;
                admin.AvatarUrl = avatarUrl;
                role = "Admin";
            }
            else
            {
                var patient = await _db.Patients.FindAsync([id.Value], HttpContext.RequestAborted);
                if (patient == null) return NotFound();

                oldAvatarUrl = patient.AvatarUrl;
                patient.AvatarData = bytes;
                patient.AvatarContentType = contentType;
                patient.AvatarUrl = avatarUrl;
                role = "Patient";
            }

            // SQL is the durable source of truth. Vercel/serverless instances can
            // recycle their local filesystem at any moment, so new avatars must not
            // depend on wwwroot/uploads. Legacy local files are removed only after
            // the durable DB commit succeeds.
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            DeleteLegacyPhysicalFileIfLocal(oldAvatarUrl);

            _logger.LogInformation("{Role} {Id} обновил аватар в постоянном хранилище", role, id);

            return Ok(new { message = "✅ Аватар обновлён", avatarUrl });
        }

        // =========================
        // ЧТЕНИЕ СОБСТВЕННОГО АВАТАРА
        // =========================
        [HttpGet("content")]
        public async Task<IActionResult> GetContent(CancellationToken cancellationToken)
        {
            var (role, id) = GetCurrentUser();
            if (id == null) return Unauthorized();

            byte[]? data;
            string? contentType;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var avatar = await _db.Admins
                    .AsNoTracking()
                    .Where(a => a.Id == id.Value)
                    .Select(a => new { a.AvatarData, a.AvatarContentType })
                    .SingleOrDefaultAsync(cancellationToken);
                if (avatar == null) return NotFound();
                data = avatar.AvatarData;
                contentType = avatar.AvatarContentType;
            }
            else if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                var avatar = await _db.Patients
                    .AsNoTracking()
                    .Where(p => p.Id == id.Value)
                    .Select(p => new { p.AvatarData, p.AvatarContentType })
                    .SingleOrDefaultAsync(cancellationToken);
                if (avatar == null) return NotFound();
                data = avatar.AvatarData;
                contentType = avatar.AvatarContentType;
            }
            else
            {
                return Forbid();
            }

            if (data == null || data.Length == 0 || string.IsNullOrWhiteSpace(contentType))
                return NotFound();

            // AvatarUrl changes on every upload, therefore this authenticated image
            // response can be cached privately for a long time without showing an
            // old avatar after replacement.
            Response.Headers["Cache-Control"] = "private, max-age=31536000, immutable";
            return File(data, contentType);
        }

        // =========================
        // УДАЛЕНИЕ АВАТАРА (возврат к иконке по умолчанию)
        // =========================
        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            var (role, id) = GetCurrentUser();
            if (id == null) return Unauthorized();

            string? oldAvatarUrl;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var admin = await _db.Admins.FindAsync([id.Value], HttpContext.RequestAborted);
                if (admin == null) return NotFound();
                oldAvatarUrl = admin.AvatarUrl;
                admin.AvatarUrl = null;
                admin.AvatarData = null;
                admin.AvatarContentType = null;
                role = "Admin";
            }
            else if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                var patient = await _db.Patients.FindAsync([id.Value], HttpContext.RequestAborted);
                if (patient == null) return NotFound();
                oldAvatarUrl = patient.AvatarUrl;
                patient.AvatarUrl = null;
                patient.AvatarData = null;
                patient.AvatarContentType = null;
                role = "Patient";
            }
            else
            {
                return Forbid();
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            DeleteLegacyPhysicalFileIfLocal(oldAvatarUrl);

            _logger.LogInformation("{Role} {Id} удалил аватар", role, id);

            return Ok(new { message = "✅ Аватар удалён" });
        }

        private static string ContentTypeForExtension(string extension) => extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        private void DeleteLegacyPhysicalFileIfLocal(string? relativeUrl)
        {
            var path = ResolveLegacyLocalAvatarPath(relativeUrl);
            if (path != null)
                DeletePhysicalPathBestEffort(path);
        }

        private string? ResolveLegacyLocalAvatarPath(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)
                || !relativeUrl.StartsWith(LegacyAvatarUrlPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var fileName = relativeUrl[LegacyAvatarUrlPrefix.Length..];
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                return null;
            }

            return Path.Combine(_env.WebRootPath, "uploads", "avatars", fileName);
        }

        private void DeletePhysicalPathBestEffort(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось удалить legacy-файл аватара {Path}", path);
            }
        }

        private (string role, int? id) GetCurrentUser()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return (role, int.TryParse(idStr, out var id) ? id : null);
        }
    }
}