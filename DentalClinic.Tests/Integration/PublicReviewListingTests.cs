using System.Net;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class PublicReviewListingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicReviewListingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Approved_ReturnsGlobalStatsButBoundsAnonymousReviewCards()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var patient = new Patient
            {
                FirstName = "PublicReviewPatient",
                Email = $"public-reviews-{Guid.NewGuid():N}@example.com",
                PasswordHash = "not-used-in-this-test"
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var moderatedBase = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 125; i++)
            {
                db.Reviews.Add(new Review
                {
                    PatientId = patient.Id,
                    Rating = (i % 5) + 1,
                    Text = $"approved-review-{i:D3}",
                    Status = "approved",
                    CreatedAt = moderatedBase.AddMinutes(i),
                    ModeratedAt = moderatedBase.AddMinutes(i)
                });
            }

            db.Reviews.Add(new Review
            {
                PatientId = patient.Id,
                Rating = 5,
                Text = "rejected-review-must-not-count",
                Status = "rejected",
                CreatedAt = moderatedBase.AddDays(1),
                ModeratedAt = moderatedBase.AddDays(1)
            });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/review/approved");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(125, root.GetProperty("count").GetInt32());
        Assert.Equal(3.0, root.GetProperty("average").GetDouble(), precision: 1);
        Assert.True(root.GetProperty("truncated").GetBoolean());

        var reviews = root.GetProperty("reviews");
        Assert.Equal(100, reviews.GetArrayLength());
        Assert.Equal("approved-review-124", reviews[0].GetProperty("text").GetString());
        Assert.Equal("approved-review-025", reviews[99].GetProperty("text").GetString());
        Assert.DoesNotContain(
            reviews.EnumerateArray(),
            review => review.GetProperty("text").GetString() == "rejected-review-must-not-count");
    }
}
