using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Services;
using Microsoft.Extensions.Configuration;

namespace DentalClinic.Tests.Unit;

public class DentaAuditDebtTests
{
    [Fact]
    public async Task DentaHandler_SafetyOverride_RejectsLegacyMedicalHeuristicsAndGuarantees()
    {
        var upstream = new CapturingHandler();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-secret"
            })
            .Build();

        var handler = new GeminiApiKeyHandler(config) { InnerHandler = upstream };
        using var client = new HttpClient(handler);

        var body = JsonSerializer.Serialize(new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = "Ты — Дента, AI-ассистент Dental Clinic. SUGGESTIONS. Анализ симптомов: боль от холодного → каналы; боль при жевании → пломба; лечение абсолютно безболезненно."
                    }
                }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "Болит зуб" } } }
            },
            generationConfig = new { temperature = 0.7 }
        });

        using var response = await client.PostAsync(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=compat",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(upstream.LastBody);
        Assert.Contains("Never diagnose", upstream.LastBody!, StringComparison.Ordinal);
        Assert.Contains("Never prescribe medicines", upstream.LastBody!, StringComparison.Ordinal);
        Assert.Contains("Do not promise that treatment will be painless", upstream.LastBody!, StringComparison.Ordinal);
        Assert.Contains("Do not delay emergency care", upstream.LastBody!, StringComparison.Ordinal);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            var structured = JsonSerializer.Serialize(new
            {
                reply = "Нужен осмотр стоматолога.",
                suggestions = new[] { "Записаться на приём" },
                links = Array.Empty<object>(),
                startBooking = false
            });

            var provider = JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[] { new { text = structured } }
                        }
                    }
                }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(provider, Encoding.UTF8, "application/json")
            };
        }
    }
}
