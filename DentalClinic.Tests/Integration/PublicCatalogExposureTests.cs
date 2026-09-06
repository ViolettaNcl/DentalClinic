using System.Net;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class PublicCatalogExposureTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicCatalogExposureTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicDoctors_ReturnOnlyActiveDoctorDtoFields()
    {
        var marker = Guid.NewGuid().ToString("N");
        var activeName = $"Public Doctor {marker}";
        var inactiveName = $"Inactive Doctor {marker}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Doctors.AddRange(
                new Doctor
                {
                    FullName = activeName,
                    FullNameEn = $"English {marker}",
                    Specialization = "Терапевтическая стоматология",
                    ExperienceYears = 8,
                    Bio = "Подтверждённый профиль",
                    IsActive = true
                },
                new Doctor
                {
                    FullName = inactiveName,
                    IsActive = false
                });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/doctor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = document.RootElement.EnumerateArray().ToArray();
        var active = rows.Single(row => row.GetProperty("fullName").GetString() == activeName);

        Assert.Equal($"English {marker}", active.GetProperty("fullNameEn").GetString());
        Assert.Equal(8, active.GetProperty("experienceYears").GetInt32());
        Assert.True(active.TryGetProperty("specialization", out _));
        Assert.True(active.TryGetProperty("bio", out _));
        Assert.False(active.TryGetProperty("isActive", out _));
        Assert.DoesNotContain(rows, row => row.GetProperty("fullName").GetString() == inactiveName);
    }

    [Fact]
    public async Task PublicServices_HideRetrievalKeywordsAndActivationState()
    {
        var marker = Guid.NewGuid().ToString("N");
        var activeName = $"Public Service {marker}";
        var inactiveName = $"Inactive Service {marker}";
        var privateKeyword = $"internal-retrieval-{marker}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Services.AddRange(
                new Service
                {
                    Category = "Public API test",
                    Name = activeName,
                    Description = "Visible description",
                    PriceFrom = 1234,
                    PriceTo = 2345,
                    Unit = "tooth",
                    Keywords = privateKeyword,
                    PageUrl = "/pages/services/fillings.html",
                    SortOrder = 0,
                    IsActive = true
                },
                new Service
                {
                    Category = "Public API test",
                    Name = inactiveName,
                    PriceFrom = 999,
                    Keywords = $"inactive-{privateKeyword}",
                    IsActive = false
                });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/service");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        var rows = document.RootElement.EnumerateArray().ToArray();
        var active = rows.Single(row => row.GetProperty("name").GetString() == activeName);

        Assert.Equal(1234m, active.GetProperty("priceFrom").GetDecimal());
        Assert.Equal(2345m, active.GetProperty("priceTo").GetDecimal());
        Assert.Equal("/pages/services/fillings.html", active.GetProperty("pageUrl").GetString());
        Assert.False(active.TryGetProperty("keywords", out _));
        Assert.False(active.TryGetProperty("isActive", out _));
        Assert.DoesNotContain(privateKeyword, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(rows, row => row.GetProperty("name").GetString() == inactiveName);
    }
}
