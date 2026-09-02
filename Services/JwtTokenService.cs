using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DentalClinic.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(int id, string email, string name, string role)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var keyValue = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key не задан в конфигурации");

        if (Encoding.UTF8.GetByteCount(keyValue) < 32)
            throw new InvalidOperationException("Jwt:Key должен быть не короче 32 байт");

        var issuer = _config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer не задан в конфигурации");
        var audience = _config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience не задан в конфигурации");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var configuredExpiry = int.TryParse(_config["Jwt:ExpiryMinutes"], out var minutes)
            ? minutes
            : 60;
        var expiryMinutes = Math.Clamp(configuredExpiry, 5, 480);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
