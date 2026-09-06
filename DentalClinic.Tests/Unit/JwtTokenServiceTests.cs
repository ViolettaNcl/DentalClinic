using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int expiryMinutes = 120)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-should-be-32-chars-min",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryMinutes"] = expiryMinutes.ToString()
            })
            .Build();

        return new JwtTokenService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsThreePartJwt()
    {
        var service = CreateService();

        var token = service.GenerateToken(1, "patient@example.com", "Иван", "Patient", 0);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length); // header.payload.signature
    }

    [Fact]
    public void GenerateToken_EmbedsExpectedClaimsIncludingTokenVersion()
    {
        var service = CreateService();

        var token = service.GenerateToken(42, "admin@example.com", "Администратор", "Admin", 7);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("admin@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("7", jwt.Claims.First(c => c.Type == JwtTokenService.TokenVersionClaim).Value);
        Assert.Equal("TestIssuer", jwt.Issuer);
    }

    [Fact]
    public void GenerateToken_SetsExpiryAccordingToConfig()
    {
        var service = CreateService(expiryMinutes: 5);

        var before = DateTime.UtcNow;
        var token = service.GenerateToken(1, "a@b.com", "Имя", "Patient", 0);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // ValidFrom не задаётся явно (нет claim "nbf"), поэтому сравниваем ValidTo
        // с ожидаемым временем по часам, а не ValidTo - ValidFrom (тот всегда
        // огромный, т.к. ValidFrom по умолчанию равен DateTime.MinValue).
        var expectedExpiry = before.AddMinutes(5);
        var diff = (jwt.ValidTo - expectedExpiry).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(10),
            $"Ожидали истечение около {expectedExpiry:o}, получили {jwt.ValidTo:o}");
    }

    [Fact]
    public void GenerateToken_MissingJwtKey_ThrowsInvalidOperationException()
    {
        var emptyConfig = new ConfigurationBuilder().Build(); // без Jwt:Key
        var service = new JwtTokenService(emptyConfig);

        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateToken(1, "a@b.com", "Имя", "Patient", 0));
    }
}
