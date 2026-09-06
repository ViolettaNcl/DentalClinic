using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ReviewTranslationSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReviewTranslationSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Translate_ApprovedReviewUsesPersistedText_NotClientSuppliedText()
    {
        var review = await SeedReviewAsync("approved", "Persisted review text from database");
        var client = _factory.CreateClient();

        // The extra `text` property represents an old/tampered client. The endpoint
        // must ignore it and load the authoritative review text from the database.
        var response = await client.PostAsJsonAsync("/api/review/translate", new
        {
            reviewId = review.Id,
            text = "ATTACKER CONTROLLED TEXT",
            targetLang = "ru"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Persisted review text from database", payload.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Translate_PendingReviewIsNotReadableByGuest()
    {
        var review = await SeedReviewAsync("pending", "Private pending review text");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/review/translate", new
        {
            reviewId = review.Id,
            targetLang = "ru"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Translate_CrossOriginRequestIsRejectedBeforeTranslation()
    {
        var review = await SeedReviewAsync("approved", "Approved review text");
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/review/translate")
        {
            Content = JsonContent.Create(new { reviewId = review.Id, targetLang = "ru" })
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Translate_MissingReviewReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/review/translate", new
        {
            reviewId = int.MaxValue,
            targetLang = "ru"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Review> SeedReviewAsync(string status, string text)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var patient = new Patient
        {
            FirstName = "ReviewTranslationOwner",
            Email = $"review-translation-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-used-in-this-test"
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var review = new Review
        {
            PatientId = patient.Id,
            Rating = 5,
            Text = text,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            ModeratedAt = status == "approved" ? DateTime.UtcNow : null
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        return review;
    }
}
