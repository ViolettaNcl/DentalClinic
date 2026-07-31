using DentalClinic.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>
/// Поднимает всё приложение (Program.cs) в памяти для интеграционных тестов:
///   - подменяет SQL Server на EF Core InMemory (реальная БД не нужна);
///   - подставляет тестовые значения Jwt:* через переменные окружения.
///
/// ВАЖНО #1: Program.cs читает Jwt:Key из конфигурации ДО builder.Build() —
/// а WebApplicationFactory встраивает свои настройки только В МОМЕНТ вызова
/// Build(), то есть позже. Поэтому Jwt:* задаются через переменные окружения:
/// это единственный источник конфигурации, который ASP.NET Core читает уже
/// в момент CreateBuilder(args).
///
/// ВАЖНО #2 (EF Core 9 breaking change): начиная с EF Core 9, AddDbContext
/// дополнительно регистрирует IDbContextOptionsConfiguration&lt;TContext&gt; —
/// и при повторном AddDbContext эта регистрация не заменяется, а добавляется
/// ВТОРОЙ записью. Если убрать только DbContextOptions&lt;ApplicationDbContext&gt;
/// (как раньше), от исходной регистрации SqlServer в Program.cs остаётся
/// "хвост" именно в виде IDbContextOptionsConfiguration — и EF Core видит сразу
/// два провайдера БД в одном контейнере ("multiple database providers have
/// been registered"). Поэтому удаляем оба типа дескрипторов.
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
