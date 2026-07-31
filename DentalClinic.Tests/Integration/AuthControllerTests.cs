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
    public async Task Register_WithValidData_ReturnsTokenAndPatientRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(UniqueEmail("patient")));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Equal("patient", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("dup");

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
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
    public async Task Login_WithCorrectCredentials_ReturnsOkAndToken()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("login");
        const string password = "password123";

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email, password));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("wrongpass");

        await client.PostAsJsonAsync("/api/auth/register", ValidRegisterRequest(email));

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
