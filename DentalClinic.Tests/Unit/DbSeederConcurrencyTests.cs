using DentalClinic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DbSeederConcurrencyTests
{
    [Fact]
    public async Task ConcurrentSeeders_DoNotDuplicateStarterRows()
    {
        var databaseName = $"db-seeder-concurrency-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);

        await Task.WhenAll(
            DbSeeder.SeedAsync(first),
            DbSeeder.SeedAsync(second));

        await using var verification = new ApplicationDbContext(options);
        var doctors = await verification.Doctors.AsNoTracking().ToListAsync();
        var services = await verification.Services.AsNoTracking().ToListAsync();

        Assert.Equal(2, doctors.Count);
        Assert.Equal(2, doctors.Select(d => d.FullName).Distinct().Count());

        Assert.Equal(23, services.Count);
        Assert.Equal(
            services.Count,
            services.Select(s => $"{s.Category}\u001F{s.Name}").Distinct().Count());
    }
}
