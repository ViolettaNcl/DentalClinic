using DentalClinic.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>
/// Поднимает всё приложение (Program.cs) в памяти для интеграционных тестов:
///   - подменяет SQL Server на EF Core InMemory (реальная БД не нужна);
///   - подставляет тестовые значения Jwt:* через переменные окружения.
///
/// ВАЖНО: Program.cs читает Jwt:Key из конфигурации ДО builder.Build() —
/// а WebApplicationFactory встраивает свои настройки (ConfigureAppConfiguration/
/// ConfigureServices) только В МОМЕНТ вызова Build(), то есть позже. Поэтому
/// добавить Jwt:Key через ConfigureAppConfiguration тут бесполезно — код в
/// Program.cs успевает упасть с "Jwt:Key не задан" раньше, чем эта настройка
/// применится. Переменные окружения — единственный источник конфигурации,
/// который ASP.NET Core читает уже в момент CreateBuilder(args), то есть
/// достаточно рано.
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
        // Реальное значение неважно — DbContext всё равно подменяется на InMemory
        // ниже, но пусть чтение ConnectionStrings:DefaultConnection в Program.cs
        // не остаётся пустым просто из аккуратности.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=unused;Database=unused;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

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
