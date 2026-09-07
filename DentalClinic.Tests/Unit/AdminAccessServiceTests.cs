using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AdminAccessServiceTests
{
    [Fact]
    public async Task RegularAdmin_CannotCreateOrManageAccounts()
    {
        await using var db = CreateDb();
        var regular = AddAdmin(db, "regular@example.com", isSuperAdmin: false);
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var created = await service.CreateAsync(
            regular.Id,
            "new@example.com",
            "NewAdmin123!",
            false);

        Assert.Equal(AdminAccessError.Forbidden, created.Error);
        Assert.Single(db.Admins);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateAdmin_ButCannotReusePatientEmail()
    {
        await using var db = CreateDb();
        var super = AddAdmin(db, "owner@example.com", isSuperAdmin: true);
        db.Patients.Add(new Patient
        {
            FirstName = "Patient",
            Email = "patient@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Patient123!")
        });
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var created = await service.CreateAsync(
            super.Id,
            "  SECOND@EXAMPLE.COM  ",
            "SecondAdmin123!",
            false);
        var conflict = await service.CreateAsync(
            super.Id,
            "patient@example.com",
            "AnotherAdmin123!",
            false);

        Assert.True(created.Succeeded);
        Assert.Equal("second@example.com", created.Admin!.Email);
        Assert.False(created.Admin.IsSuperAdmin);
        Assert.Equal(AdminAccessError.PatientEmailConflict, conflict.Error);
    }

    [Fact]
    public async Task LastSuperAdmin_CannotBeDemoted()
    {
        await using var db = CreateDb();
        var owner = AddAdmin(db, "owner@example.com", isSuperAdmin: true);
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var result = await service.SetSuperAdminAsync(owner.Id, owner.Id, false);

        Assert.Equal(AdminAccessError.LastSuperAdmin, result.Error);
        Assert.True((await db.Admins.SingleAsync()).IsSuperAdmin);
    }

    [Fact]
    public async Task PrivilegeChange_RevokesTargetSessions()
    {
        await using var db = CreateDb();
        var owner = AddAdmin(db, "owner@example.com", isSuperAdmin: true);
        var second = AddAdmin(db, "second@example.com", isSuperAdmin: false);
        second.TokenVersion = 4;
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var promoted = await service.SetSuperAdminAsync(owner.Id, second.Id, true);

        Assert.True(promoted.Succeeded);
        Assert.True(second.IsSuperAdmin);
        Assert.Equal(5, second.TokenVersion);
    }

    [Fact]
    public async Task PasswordReset_ChangesHashAndRevokesTargetSessions()
    {
        await using var db = CreateDb();
        var owner = AddAdmin(db, "owner@example.com", isSuperAdmin: true);
        var second = AddAdmin(db, "second@example.com", isSuperAdmin: false);
        second.TokenVersion = 2;
        var previousHash = second.PasswordHash;
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var result = await service.ResetPasswordAsync(owner.Id, second.Id, "Replacement123!");

        Assert.True(result.Succeeded);
        Assert.NotEqual(previousHash, second.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Replacement123!", second.PasswordHash));
        Assert.Equal(3, second.TokenVersion);
    }

    [Fact]
    public async Task SuperAdmin_CannotDeleteOwnActiveAccount()
    {
        await using var db = CreateDb();
        var owner = AddAdmin(db, "owner@example.com", isSuperAdmin: true);
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var result = await service.DeleteAsync(owner.Id, owner.Id);

        Assert.Equal(AdminAccessError.CannotDeleteSelf, result.Error);
        Assert.Single(db.Admins);
    }

    [Fact]
    public async Task ConcurrentCrossDemotions_CannotLeaveZeroSuperAdmins()
    {
        var dbName = $"admin-access-race-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        int firstId;
        int secondId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var first = AddAdmin(seed, "first@example.com", isSuperAdmin: true);
            var second = AddAdmin(seed, "second@example.com", isSuperAdmin: true);
            await seed.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }

        await using var db1 = new ApplicationDbContext(options);
        await using var db2 = new ApplicationDbContext(options);
        var service1 = new AdminAccessService(db1);
        var service2 = new AdminAccessService(db2);

        var results = await Task.WhenAll(
            service1.SetSuperAdminAsync(firstId, secondId, false),
            service2.SetSuperAdminAsync(secondId, firstId, false));

        await using var verify = new ApplicationDbContext(options);
        var remaining = await verify.Admins.CountAsync(a => a.IsSuperAdmin);

        Assert.Equal(1, remaining);
        Assert.Contains(results, result => result.Succeeded);
        Assert.Contains(results, result => result.Error is AdminAccessError.Forbidden or AdminAccessError.LastSuperAdmin);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-access-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Admin AddAdmin(ApplicationDbContext db, string email, bool isSuperAdmin)
    {
        var admin = new Admin
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword123!"),
            IsSuperAdmin = isSuperAdmin
        };
        db.Admins.Add(admin);
        return admin;
    }
}