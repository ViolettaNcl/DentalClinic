using DentalClinic.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>
/// Поднимает всё приложение (Program.cs) в памяти для интеграционных тестов:
///   - подменяет SQL Server на EF Core InMemory (реальная БД не нужна);
///   - подставляет тестовые значения Jwt:*, которых иначе не было бы
///     в окружении CI и приложение бы упало на старте с InvalidOperationException.
/// Каждый экземпляр фабрики использует свою уникальную in-memory базу,
/// так что тесты не мешают друг другу.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"dentalclinic-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-signing-key-32-chars-minimum",
                ["Jwt:Issuer"] = "DentalClinicTests",
                ["Jwt:Audience"] = "DentalClinicTestsClient",
                ["Jwt:ExpiryMinutes"] = "120",
                ["ConnectionStrings:DefaultConnection"] = "unused-because-overridden-below",
                ["AllowedOrigins:0"] = "http://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
