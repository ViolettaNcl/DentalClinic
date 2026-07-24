using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DentalClinic.Services;

// Отвечает только за выпуск JWT — проверка токена на входящих запросах
// настраивается отдельно в Program.cs (AddAuthentication().AddJwtBearer(...)).
public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Выпускает подписанный JWT для авторизованного пользователя (пациент/врач/админ).
    /// Роль кладём прямо в claims, чтобы [Authorize(Roles = "Admin")] на контроллерах
    /// проверялся из самого токена — без похода в БД на каждый запрос.
    /// </summary>
    public string GenerateToken(int id, string email, string name, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role)
        };

        var keyValue = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key не задан в конфигурации");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 120;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}