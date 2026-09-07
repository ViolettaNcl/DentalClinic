using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AdminAccessControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminAccessControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegularAdmin_CannotReadAdminAccessRegistry()
    {
        var (email, password, _) = await SeedAdminAsync(isSuperAdmin: false);
        var client = _factory.CreateClient();
        await LoginAdminAsync(client, email, password);

        var response = await client.GetAsync("/api/admin-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateOperationalAdmin_ThatCanLoginNormally()
    {
        var (ownerEmail, ownerPassword, _) = await SeedAdminAsync(isSuperAdmin: true);
        var client = _factory.CreateClient();
        await LoginAdminAsync(client, ownerEmail, ownerPassword);

        var newEmail = UniqueEmail("created-admin");
        const string newPassword = "CreatedAdmin123!";
        var created = await client.PostAsJsonAsync("/api/admin-access", new CreateAdminAccountRequest
        {
            Email = newEmail,
            Password = newPassword,
            IsSuperAdmin = false
        });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(newEmail, createdBody.GetProperty("email").GetString());
        Assert.False(createdBody.GetProperty("isSuperAdmin").GetBoolean());

        var loginClient = _factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = newEmail,
            Password = newPassword
        });
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("admin", loginBody.GetProperty("role").GetString());
    }

    [Fact]
    public async Task SuperAdmin_CannotDemoteTheOnlySuperAdmin()
    {
        var (email, password, adminId) = await SeedAdminAsync(isSuperAdmin: true);
        var client = _factory.CreateClient();
        await LoginAdminAsync(client, email, password);

        var response = await client.PutAsJsonAsync(
            $"/api/admin-access/{adminId}/super-admin",
            new SetSuperAdminRequest { IsSuperAdmin = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_WeakPasswordForNewAdmin_IsRejected()
    {
        var (email, password, _) = await SeedAdminAsync(isSuperAdmin: true);
        var client = _factory.CreateClient();
        await LoginAdminAsync(client, email, password);

        var response = await client.PostAsJsonAsync("/api/admin-access", new CreateAdminAccountRequest
        {
            Email = UniqueEmail("weak-admin"),
            Password = "password123!",
            IsSuperAdmin = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(string Email, string Password, int Id)> SeedAdminAsync(bool isSuperAdmin)
    {
        var email = UniqueEmail(isSuperAdmin ? "super" : "regular");
        var password = "AdminTestPassword1!";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = new Admin
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsSuperAdmin = isSuperAdmin
        };
        db.Admins.Add(admin);
        await db.SaveChangesAsync();
        return (email, password, admin.Id);
    }

    private static async Task LoginAdminAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string UniqueEmail(string prefix)
        => $"{prefix}-{Guid.NewGuid():N}@example.com";
}