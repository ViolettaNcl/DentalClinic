using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ChatAdminSessionRecencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ChatAdminSessionRecencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RecentSessions_UsesLastActivityForSelectionAndOrdering()
    {
        var adminEmail = $"chat-admin-{Guid.NewGuid():N}@example.test";
        const string password = "chat-admin-password-123";
        var suffix = Guid.NewGuid().ToString("N");
        var resumedSession = $"resumed-{suffix}";
        var newerStartSession = $"newer-start-{suffix}";
        var olderSession = $"older-{suffix}";

        var resumedStartedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
        var resumedLastActivityAt = new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc);
        var newerStartAt = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var olderAt = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });

            // Deliberately insert rows out of chronological order. The resumed
            // session began first, but its newest message is the most recent event.
            db.ChatMessageLogs.AddRange(
                new ChatMessageLog
                {
                    SessionId = newerStartSession,
                    Role = "user",
                    Text = "newer-start-message",
                    Lang = "en",
                    CreatedAt = newerStartAt
                },
                new ChatMessageLog
                {
                    SessionId = resumedSession,
                    Role = "bot",
                    Text = "resumed-latest-reply",
                    Lang = "en",
                    CreatedAt = resumedLastActivityAt
                },
                new ChatMessageLog
                {
                    SessionId = olderSession,
                    Role = "user",
                    Text = "older-message",
                    Lang = "en",
                    CreatedAt = olderAt
                },
                new ChatMessageLog
                {
                    SessionId = resumedSession,
                    Role = "user",
                    Text = "resumed-first-question",
                    Lang = "en",
                    CreatedAt = resumedStartedAt
                });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = adminEmail,
            password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await client.GetAsync("/api/chat/admin/sessions?take=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessions = json.RootElement;
        Assert.Equal(JsonValueKind.Array, sessions.ValueKind);
        Assert.Equal(2, sessions.GetArrayLength());

        var first = sessions[0];
        Assert.Equal(resumedSession, first.GetProperty("sessionId").GetString());
        Assert.Equal(resumedStartedAt, first.GetProperty("startedAt").GetDateTime());
        Assert.Equal(resumedLastActivityAt, first.GetProperty("lastActivityAt").GetDateTime());
        Assert.Equal(2, first.GetProperty("messageCount").GetInt32());
        Assert.Equal("resumed-first-question", first.GetProperty("preview").GetString());

        var messages = first.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("resumed-first-question", messages[0].GetProperty("text").GetString());
        Assert.Equal("resumed-latest-reply", messages[1].GetProperty("text").GetString());

        var second = sessions[1];
        Assert.Equal(newerStartSession, second.GetProperty("sessionId").GetString());
        Assert.Equal(newerStartAt, second.GetProperty("lastActivityAt").GetDateTime());

        Assert.DoesNotContain(
            sessions.EnumerateArray(),
            session => session.GetProperty("sessionId").GetString() == olderSession);
    }

    [Fact]
    public async Task RecentSessions_RequiresAdminAuthorization()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/chat/admin/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
