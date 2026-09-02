using DentalClinic.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>
/// In-memory integration host. Production secrets/providers are replaced with deterministic
/// test configuration so API/security tests never call external AI or the live database.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"dentalclinic-tests-{Guid.NewGuid()}";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-32-chars-minimum");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "DentalClinicTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "DentalClinicTestsClient");
        Environment.SetEnvironmentVariable("Jwt__ExpiryMinutes", "120");
        Environment.SetEnvironmentVariable("AllowedOrigins__0", "http://localhost");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=unused;Database=unused;");
        Environment.SetEnvironmentVariable("Gemini__ApiKey", string.Empty);
        Environment.SetEnvironmentVariable("CRON_SECRET", "integration-test-cron-secret");
        Environment.SetEnvironmentVariable("ChatRetention__MessageDays", "30");
        Environment.SetEnvironmentVariable("ChatRetention__IpDays", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(IDbContextOptionsConfiguration<ApplicationDbContext>))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
