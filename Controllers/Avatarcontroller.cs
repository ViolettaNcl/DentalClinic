using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DentalClinic.Controllers
{
    // Загрузка и удаление аватара — общий эндпоинт и для админа, и для пациента:
    // роль читаем из JWT-токена и обновляем нужную таблицу (Admins или Patients).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AvatarController : ControllerBase
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSizeBytes = 3 * 1024 * 1024; // 3 МБ
        private const string AvatarUrlPrefix = "/uploads/avatars/";

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
            // до записи в wwwroot, чтобы произвольный HTML/скрипт с именем .png
            // не оказался среди публично доступных загрузок.
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

            // Сначала убеждаемся, что аккаунт действительно существует. Раньше файл
            // записывался на диск до FindAsync(), поэтому устаревший/некорректный JWT
            // мог оставлять сиротские файлы даже при последующем 404.
            string? oldAvatarUrl;
            Action<string> assignAvatarUrl;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var admin = await _db.Admins.FindAsync(id.Value);
                if (admin == null) return NotFound();
                oldAvatarUrl = admin.AvatarUrl;
                assignAvatarUrl = url => admin.AvatarUrl = url;
                role = "Admin";
            }
            else
            {
                var patient = await _db.Patients.FindAsync(id.Value);
                if (patient == null) return NotFound();
                oldAvatarUrl = patient.AvatarUrl;
                assignAvatarUrl = url => patient.AvatarUrl = url;
                role = "Patient";
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{role.ToLowerInvariant()}-{id}-{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            var relativeUrl = $"{AvatarUrlPrefix}{fileName}";

            try
            {
                await using (var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await file.CopyToAsync(stream, HttpContext.RequestAborted);
                }

                assignAvatarUrl(relativeUrl);
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }
            catch
            {
                // Файловая система и БД не участвуют в одной транзакции. Если запись
                // нового файла или SaveChanges завершается ошибкой, удаляем частичный/
                // новый файл и не трогаем прежний аватар.
                DeletePhysicalPathBestEffort(filePath);
                throw;
            }

            DeletePhysicalFileIfLocal(oldAvatarUrl);

            _logger.LogInformation("{Role} {Id} обновил аватар", role, id);

            return Ok(new { message = "✅ Аватар обновлён", avatarUrl = relativeUrl });
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
                var admin = await _db.Admins.FindAsync(id.Value);
                if (admin == null) return NotFound();
                oldAvatarUrl = admin.AvatarUrl;
                admin.AvatarUrl = null;
                role = "Admin";
            }
            else if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                var patient = await _db.Patients.FindAsync(id.Value);
                if (patient == null) return NotFound();
                oldAvatarUrl = patient.AvatarUrl;
                patient.AvatarUrl = null;
                role = "Patient";
            }
            else
            {
                return Forbid();
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            DeletePhysicalFileIfLocal(oldAvatarUrl);

            _logger.LogInformation("{Role} {Id} удалил аватар", role, id);

            return Ok(new { message = "✅ Аватар удалён" });
        }

        private void DeletePhysicalFileIfLocal(string? relativeUrl)
        {
            var path = ResolveLocalAvatarPath(relativeUrl);
            if (path != null)
                DeletePhysicalPathBestEffort(path);
        }

        private string? ResolveLocalAvatarPath(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)
                || !relativeUrl.StartsWith(AvatarUrlPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            // Храним только сгенерированное имя файла, а не произвольный относительный
            // путь. Это не позволяет старому/повреждённому AvatarUrl с ../ выйти за
            // wwwroot/uploads/avatars при удалении.
            var fileName = relativeUrl[AvatarUrlPrefix.Length..];
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
                _logger.LogWarning(ex, "Не удалось удалить файл аватара {Path}", path);
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