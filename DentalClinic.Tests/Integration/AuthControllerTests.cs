using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest ValidRegisterRequest(string email, string password = "password123") => new()
    {
        FirstName = "Тест",
        Email = email,
        Password = password
    };

    private static string UniqueEmail(string prefix) => $"{prefix}_{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_WithValidData_SetsHttpOnlyCookieAndPatientRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(UniqueEmail("patient")));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("patient", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("token", out _));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("dc_auth=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CookieSession_CanAccessProtectedProfile()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("cookie-profile");

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));
        var profile = await client.GetAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Logout_ExpiresAuthenticationCookie()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(UniqueEmail("logout")));

        var response = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.Contains("dc_auth=", StringComparison.OrdinalIgnoreCase)
                && (value.Contains("expires=", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequestOrConflict()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("dup");

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));

        Assert.True(second.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_PasswordTooShort_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRegisterRequest(UniqueEmail("shortpass"), password: "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmailFormat_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRegisterRequest("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsCookieWithoutTokenBody()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("login");
        const string password = "password123";

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email, password));
        await client.PostAsync("/api/auth/logout", null);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.TryGetProperty("token", out _));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("dc_auth=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("wrongpass");

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));
        await client.PostAsync("/api/auth/logout", null);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "totally-wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = UniqueEmail("nosuchuser"),
            Password = "whatever123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
