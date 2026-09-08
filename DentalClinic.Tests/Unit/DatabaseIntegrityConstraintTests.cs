using DentalClinic.Data;
using DentalClinic.Migrations;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DatabaseIntegrityConstraintTests
{
    [Fact]
    public void EfModel_DeclaresReviewDomainConstraints()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var constraints = model.FindEntityType(typeof(Review))!
            .GetCheckConstraints()
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_Reviews_Rating", constraints);
        Assert.Contains("CK_Reviews_Status", constraints);
    }

    [Fact]
    public void EfModel_DeclaresDoctorAndServiceDomainConstraints()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;

        var doctorConstraints = model.FindEntityType(typeof(Doctor))!
            .GetCheckConstraints()
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CK_Doctors_ExperienceYears", doctorConstraints);

        var serviceConstraints = model.FindEntityType(typeof(Service))!
            .GetCheckConstraints()
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CK_Services_PriceRange", serviceConstraints);
        Assert.Contains("CK_Services_SortOrder", serviceConstraints);
    }

    [Fact]
    public void Migration_RejectsInvalidLegacyRowsBeforeAddingConstraints()
    {
        var migration = new TestableDomainIntegrityMigration();
        var sql = string.Join(
            "\n",
            migration.BuildUpOperations().OfType<SqlOperation>().Select(op => op.Sql));

        Assert.Contains("THROW 51020", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51021", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51022", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51023", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51024", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Reviews_Rating]", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Reviews_Status]", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Doctors_ExperienceYears]", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Services_PriceRange]", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK ADD CONSTRAINT [CK_Services_SortOrder]", sql, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"domain-integrity-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestableDomainIntegrityMigration : AddDomainIntegrityConstraints
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }
    }
}
