using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ReviewModerationIdempotencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReviewModerationIdempotencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Moderate_ReplayedApproval_DoesNotDuplicateNotificationOrResetReadState()
    {
        var (patientClient, reviewId) = await CreatePatientReviewAsync();
        var adminClient = await CreateAdminClientAsync();

        var first = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = "approved"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var markRead = await patientClient.PostAsync($"/api/review/{reviewId}/mark-read", null);
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);

        DateTime? moderatedAt;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var review = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
            moderatedAt = review.ModeratedAt;
            Assert.True(review.IsNotificationRead);
            Assert.Equal(1, await db.Notifications.CountAsync(n =>
                n.RelatedId == reviewId && n.Type == "review_approved"));
        }

        var replay = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = " APPROVED "
        });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBody = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayBody.GetProperty("idempotent").GetBoolean());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verifyDb.Reviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        Assert.Equal(moderatedAt, saved.ModeratedAt);
        Assert.True(saved.IsNotificationRead);
        Assert.Equal(1, await verifyDb.Notifications.CountAsync(n =>
            n.RelatedId == reviewId && n.Type == "review_approved"));
    }

    [Fact]
    public async Task Moderate_ReplayedRejectionWithEquivalentTrimmedReason_IsIdempotent()
    {
        var (patientClient, reviewId) = await CreatePatientReviewAsync();
        var adminClient = await CreateAdminClientAsync();

        var first = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = "rejected",
            RejectionReason = "  Недостаточно информации  "
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var markRead = await patientClient.PostAsync($"/api/review/{reviewId}/mark-read", null);
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);

        var replay = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = "REJECTED",
            RejectionReason = "Недостаточно информации"
        });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBody = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayBody.GetProperty("idempotent").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        Assert.True(saved.IsNotificationRead);
        Assert.Equal("Недостаточно информации", saved.RejectionReason);
        Assert.Equal(1, await db.Notifications.CountAsync(n =>
            n.RelatedId == reviewId && n.Type == "review_rejected"));
    }

    [Fact]
    public async Task Moderate_RealStatusChange_RemainsARealTransitionAndNotifiesAgain()
    {
        var (_, reviewId) = await CreatePatientReviewAsync();
        var adminClient = await CreateAdminClientAsync();

        var approve = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = "approved"
        });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var reject = await adminClient.PutAsJsonAsync($"/api/review/admin/{reviewId}/moderate", new ModerateReviewRequest
        {
            Status = "rejected",
            RejectionReason = "Новая проверка"
        });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var body = await reject.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("idempotent").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        Assert.Equal("rejected", saved.Status);
        Assert.Equal("Новая проверка", saved.RejectionReason);
        Assert.False(saved.IsNotificationRead);
        Assert.Equal(1, await db.Notifications.CountAsync(n =>
            n.RelatedId == reviewId && n.Type == "review_approved"));
        Assert.Equal(1, await db.Notifications.CountAsync(n =>
            n.RelatedId == reviewId && n.Type == "review_rejected"));
    }

    private async Task<(HttpClient patientClient, int reviewId)> CreatePatientReviewAsync()
    {
        var client = _factory.CreateClient();
        var email = $"moderation-patient-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "ReviewPatient",
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var create = await client.PostAsJsonAsync("/api/review", new CreateReviewRequest
        {
            Rating = 5,
            Text = "Это достаточно длинный тестовый отзыв пациента."
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("id").GetInt32());
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = $"moderation-admin-{Guid.NewGuid():N}@example.com";
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
