using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ReviewReadPaginationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReviewReadPaginationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task AdminPagedFeed_ReturnsBoundedPageAndFullCount()
    {
        var patientEmail = UniqueEmail("review-page-patient");
        var adminEmail = UniqueEmail("review-page-admin");
        const string adminPassword = "admin-test-password";
        int patientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var patient = new Patient
            {
                FirstName = "Pagination",
                Email = patientEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            db.Patients.Add(patient);
            db.Admins.Add(new Admin
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword)
            });
            await db.SaveChangesAsync();
            patientId = patient.Id;

            var now = DateTime.UtcNow.AddDays(2);
            db.Reviews.AddRange(Enumerable.Range(0, 37).Select(i => new Review
            {
                PatientId = patientId,
                Rating = 5,
                Text = $"paged-review-{i:D2}",
                Status = "approved",
                CreatedAt = now.AddMinutes(-i),
                ModeratedAt = now.AddMinutes(-i)
            }));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = adminEmail,
            Password = adminPassword
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await client.GetAsync("/api/review/admin/list/approved?page=2&pageSize=15");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(15, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("total").GetInt32() >= 37);
        Assert.True(body.GetProperty("items").GetArrayLength() <= 15);
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 3);
    }

    [Fact]
    public async Task LegacyAdminFeed_IsCappedAndSignalsTruncation()
    {
        var patientEmail = UniqueEmail("review-legacy-patient");
        var adminEmail = UniqueEmail("review-legacy-admin");
        const string adminPassword = "admin-test-password";
        int patientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var patient = new Patient
            {
                FirstName = "LegacyCap",
                Email = patientEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            db.Patients.Add(patient);
            db.Admins.Add(new Admin
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword)
            });
            await db.SaveChangesAsync();
            patientId = patient.Id;

            var now = DateTime.UtcNow.AddDays(3);
            db.Reviews.AddRange(Enumerable.Range(0, 205).Select(i => new Review
            {
                PatientId = patientId,
                Rating = 4,
                Text = $"legacy-cap-{i:D3}",
                Status = "pending",
                CreatedAt = now.AddSeconds(-i)
            }));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = adminEmail,
            Password = adminPassword
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await client.GetAsync("/api/review/admin/pending");
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(200, rows.GetArrayLength());
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Result-Truncated")));
    }

    [Fact]
    public async Task PatientReviewFeed_IsCappedAndSignalsTruncation()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("review-patient-cap");
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "PatientCap",
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        var patientId = session.GetProperty("id").GetInt32();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow.AddDays(4);
            db.Reviews.AddRange(Enumerable.Range(0, 205).Select(i => new Review
            {
                PatientId = patientId,
                Rating = 3,
                Text = $"patient-cap-{i:D3}",
                Status = "approved",
                CreatedAt = now.AddSeconds(-i),
                ModeratedAt = now.AddSeconds(-i)
            }));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/review/patient/{patientId}");
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(200, rows.GetArrayLength());
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Result-Truncated")));
    }
}
