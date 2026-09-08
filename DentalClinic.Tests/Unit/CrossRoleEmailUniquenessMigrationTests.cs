using Xunit;

namespace DentalClinic.Tests.Unit;

public class CrossRoleEmailUniquenessMigrationTests
{
    [Fact]
    public void Migration_FailsClosedAndSerializesBothIdentityWritePaths()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../Migrations/20260909002000_EnforceCrossRoleEmailUniqueness.cs"));
        var source = File.ReadAllText(path);

        Assert.Contains("THROW 51030", source);
        Assert.Contains("TR_Patients_EnforceCrossRoleEmailUniqueness", source);
        Assert.Contains("TR_Admins_EnforceCrossRoleEmailUniqueness", source);
        Assert.Contains("INNER JOIN [dbo].[Admins] a ON a.[Email] = i.[Email]", source);
        Assert.Contains("INNER JOIN [dbo].[Patients] p ON p.[Email] = i.[Email]", source);
        Assert.Contains("@LockOwner = ''Transaction''", source);
        Assert.Contains("@LockMode = ''Exclusive''", source);

        Assert.Equal(
            2,
            CountOccurrences(source, "@Resource = N''DentalClinic.IdentityEmail''"));

        // The migration must never silently decide whether a patient or admin row
        // should win when legacy data is already inconsistent.
        Assert.DoesNotContain("UPDATE [dbo].[Patients]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [dbo].[Admins]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Patients]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Admins]", source, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
