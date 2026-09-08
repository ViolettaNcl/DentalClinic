using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DatabaseInvariantConstraintTests
{
    [Fact]
    public void EfModel_DeclaresCriticalDomainCheckConstraints()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"db-invariants-{Guid.NewGuid():N}")
            .Options;

        using var db = new ApplicationDbContext(options);

        AssertConstraint(db, typeof(Review), "CK_Reviews_Rating", "BETWEEN 1 AND 5");
        AssertConstraint(db, typeof(Review), "CK_Reviews_Status", "'pending', 'approved', 'rejected'");
        AssertConstraint(db, typeof(Doctor), "CK_Doctors_ExperienceYears", "BETWEEN 0 AND 80");
        AssertConstraint(db, typeof(Service), "CK_Services_PriceRange", "[PriceTo] >= [PriceFrom]");
        AssertConstraint(db, typeof(Service), "CK_Services_SortOrder", "[SortOrder] >= 0");
    }

    [Fact]
    public void Migration_FailsClosedOnLegacyViolationsBeforeAddingConstraints()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../Migrations/20260909001200_AddDomainInvariantChecks.cs"));
        var source = File.ReadAllText(path);

        Assert.Contains("THROW 51020", source);
        Assert.Contains("THROW 51021", source);
        Assert.Contains("THROW 51022", source);
        Assert.Contains("THROW 51023", source);
        Assert.Contains("THROW 51024", source);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Reviews_Rating]", source);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Doctors_ExperienceYears]", source);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Services_PriceRange]", source);

        Assert.DoesNotContain("UPDATE [dbo].[Reviews]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [dbo].[Doctors]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [dbo].[Services]", source, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertConstraint(
        ApplicationDbContext db,
        Type entityType,
        string name,
        string sqlFragment)
    {
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var constraint = Assert.Single(entity!.GetCheckConstraints(), c => c.Name == name);
        Assert.Contains(sqlFragment, constraint.Sql, StringComparison.Ordinal);
    }
}
