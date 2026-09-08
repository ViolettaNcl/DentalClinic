using Xunit;

namespace DentalClinic.Tests.Unit;

public class CrossRoleEmailUniquenessMigrationTests
{
    [Fact]
    public void Migration_FailsClosedWithoutInstallingWriteTriggers()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../Migrations/20260909002000_EnforceCrossRoleEmailUniqueness.cs"));
        var source = File.ReadAllText(path);

        Assert.Contains("THROW 51030", source);
        Assert.Contains("INNER JOIN [dbo].[Admins] a ON a.[Email] = p.[Email]", source);
        Assert.DoesNotContain("CREATE OR ALTER TRIGGER", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [dbo].[Patients]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [dbo].[Admins]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Patients]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Admins]", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeGuard_AcquiresOneTransactionOwnedLockBeforeIdentityChecks()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var guard = File.ReadAllText(Path.Combine(root, "Services/IdentityEmailGuard.cs"));
        var auth = File.ReadAllText(Path.Combine(root, "Controllers/AuthController.cs"));
        var adminAccess = File.ReadAllText(Path.Combine(root, "Services/AdminAccessService.cs"));

        Assert.Contains("@Resource = N'DentalClinic.IdentityEmail'", guard);
        Assert.Contains("@LockMode = 'Exclusive'", guard);
        Assert.Contains("@LockOwner = 'Transaction'", guard);
        Assert.Contains("CurrentTransaction", guard);
        Assert.Contains("IdentityEmailGuard.ExecuteSerializedAsync(_db", auth);
        Assert.Contains("_db.Admins.AnyAsync(a => a.Email == email", auth);
        Assert.Contains("IdentityEmailGuard.ExecuteSerializedAsync(_db", adminAccess);
    }
}
