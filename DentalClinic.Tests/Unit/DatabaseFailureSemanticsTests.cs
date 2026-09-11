using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DatabaseFailureSemanticsTests
{
    [Fact]
    public async Task PatientRegistration_UnrelatedDatabaseFailure_IsNotReportedAsDuplicateEmail()
    {
        await using var db = CreateDb();
        db.FailSaves = true;
        var controller = new AuthController(
            db,
            CreateTokenService(),
            NullLogger<AuthController>.Instance,
            null!);

        await Assert.ThrowsAsync<DbUpdateException>(() => controller.Register(
            new RegisterRequest
            {
                FirstName = "Failure test",
                Email = "new-patient@example.test",
                Password = "Password123!"
            },
            CancellationToken.None));

        Assert.Empty(db.Patients.Local);
        Assert.Empty(db.Patients);
    }

    [Fact]
    public async Task AdminCreation_UnrelatedDatabaseFailure_IsNotReportedAsDuplicateEmail()
    {
        await using var db = CreateDb();
        var owner = new Admin
        {
            Email = "owner@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OwnerPassword123!"),
            IsSuperAdmin = true
        };
        db.Admins.Add(owner);
        await db.SaveChangesAsync();
        db.FailSaves = true;
        var service = new AdminAccessService(db);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(
            owner.Id,
            "new-admin@example.test",
            "AdminPassword123!",
            false));

        Assert.Single(db.Admins.Local);
        Assert.Single(db.Admins);
    }

    private static JwtTokenService CreateTokenService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long",
                ["Jwt:Issuer"] = "DentalClinic.Tests",
                ["Jwt:Audience"] = "DentalClinic.Tests"
            })
            .Build();

        return new JwtTokenService(configuration);
    }

    private static ControlledSaveApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"failure-semantics-{Guid.NewGuid():N}")
            .Options;
        return new ControlledSaveApplicationDbContext(options);
    }

    private sealed class ControlledSaveApplicationDbContext : ApplicationDbContext
    {
        public bool FailSaves { get; set; }

        public ControlledSaveApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => FailSaves
                ? Task.FromException<int>(new DbUpdateException("Simulated unrelated storage failure."))
                : base.SaveChangesAsync(cancellationToken);
    }
}
