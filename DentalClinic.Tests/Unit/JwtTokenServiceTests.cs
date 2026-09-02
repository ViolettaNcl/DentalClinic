using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int expiryMinutes = 60)
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
        var token = CreateService().GenerateToken(1, "patient@example.com", "Иван", "Patient");
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_EmbedsMinimalExpectedClaims()
    {
        var token = CreateService().GenerateToken(42, "admin@example.com", "Администратор", "Admin");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Email);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        Assert.True(jwt.ValidFrom > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void GenerateToken_SetsExpiryAccordingToConfig()
    {
        var before = DateTime.UtcNow;
        var token = CreateService(expiryMinutes: 5).GenerateToken(1, "a@b.com", "Имя", "Patient");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expectedExpiry = before.AddMinutes(5);
        var diff = (jwt.ValidTo - expectedExpiry).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GenerateToken_ClampsExcessivelyLongExpiry()
    {
        var token = CreateService(expiryMinutes: 10000).GenerateToken(1, "a@b.com", "Имя", "Patient");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(481));
    }

    [Fact]
    public void GenerateToken_MissingJwtKey_ThrowsInvalidOperationException()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new JwtTokenService(emptyConfig);
        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateToken(1, "a@b.com", "Имя", "Patient"));
    }
}
