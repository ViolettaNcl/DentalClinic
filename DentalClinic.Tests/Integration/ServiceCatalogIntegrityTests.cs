using System.Net;
using System.Net.Http.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ServiceCatalogIntegrityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceCatalogIntegrityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_DuplicateActivePageSlot_ReturnsConflict()
    {
        var pageUrl = $"/pages/services/test-{Guid.NewGuid():N}.html";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Services.Add(new Service
            {
                Category = "Test",
                Name = "Existing",
                PriceFrom = 100,
                PageUrl = pageUrl,
                SortOrder = 1,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/service", new
        {
            category = "Test",
            name = "Conflicting",
            priceFrom = 200,
            pageUrl,
            sortOrder = 1
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_CanReuseSlotHeldOnlyByInactiveService()
    {
        var pageUrl = $"/pages/services/test-{Guid.NewGuid():N}.html";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Services.Add(new Service
            {
                Category = "Test",
                Name = "Inactive",
                PriceFrom = 100,
                PageUrl = pageUrl,
                SortOrder = 1,
                IsActive = false
            });
            await db.SaveChangesAsync();
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/service", new
        {
            category = "Test",
            name = "Replacement",
            priceFrom = 200,
            pageUrl,
            sortOrder = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ActivatingDuplicatePageSlot_ReturnsConflictAndPreservesInactiveState()
    {
        var pageUrl = $"/pages/services/test-{Guid.NewGuid():N}.html";
        int inactiveId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var active = new Service
            {
                Category = "Test",
                Name = "Active",
                PriceFrom = 100,
                PageUrl = pageUrl,
                SortOrder = 2,
                IsActive = true
            };
            var inactive = new Service
            {
                Category = "Test",
                Name = "Inactive",
                PriceFrom = 200,
                PageUrl = pageUrl,
                SortOrder = 2,
                IsActive = false
            };
            db.Services.AddRange(active, inactive);
            await db.SaveChangesAsync();
            inactiveId = inactive.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/service/{inactiveId}", new
        {
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.Services.FindAsync(inactiveId);
        Assert.NotNull(saved);
        Assert.False(saved!.IsActive);
    }

    [Fact]
    public async Task Create_NegativeSortOrder_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/service", new
        {
            category = "Test",
            name = "Invalid order",
            priceFrom = 100,
            pageUrl = $"/pages/services/test-{Guid.NewGuid():N}.html",
            sortOrder = -1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var email = $"admin-service-{Guid.NewGuid():N}@example.com";
        const string password = "admin-test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
