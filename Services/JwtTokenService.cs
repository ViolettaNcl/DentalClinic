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

    public int ExpiryMinutes => int.TryParse(_config["Jwt:ExpiryMinutes"], out var m)
        ? Math.Clamp(m, 5, 24 * 60)
        : 120;

    public DateTime GetExpiryUtc() => DateTime.UtcNow.AddMinutes(ExpiryMinutes);

    public string GenerateToken(int id, string email, string name, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var keyValue = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key не задан в конфигурации");

        if (Encoding.UTF8.GetByteCount(keyValue) < 32)
            throw new InvalidOperationException("Jwt:Key должен содержать не менее 32 байт энтропии");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(ExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
