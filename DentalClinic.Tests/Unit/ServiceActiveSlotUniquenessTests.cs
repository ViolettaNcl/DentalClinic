using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ServiceActiveSlotUniquenessTests
{
    [Fact]
    public void EfModel_DeclaresFilteredUniqueActivePageSlotIndex()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"service-slot-model-{Guid.NewGuid():N}")
            .Options;
        using var db = new ApplicationDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Service));
        Assert.NotNull(entity);

        var index = Assert.Single(entity!.GetIndexes(), candidate =>
            candidate.GetDatabaseName() == "UX_Services_ActivePageSlot");

        Assert.True(index.IsUnique);
        Assert.Equal(new[] { nameof(Service.PageUrl), nameof(Service.SortOrder) },
            index.Properties.Select(property => property.Name));
        Assert.Equal(
            "[IsActive] = 1 AND [PageUrl] IS NOT NULL AND [SortOrder] > 0",
            index.GetFilter());
    }

    [Fact]
    public void Migration_FailsClosedBeforeCreatingActivePageSlotIndex()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(
            root,
            "Migrations/20260909012000_EnforceActiveServicePageSlotUniqueness.cs"));

        Assert.Contains("THROW 51033", source, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) > 1", source, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [UX_Services_ActivePageSlot]", source, StringComparison.Ordinal);
        Assert.Contains("WHERE [IsActive] = 1 AND [PageUrl] IS NOT NULL AND [SortOrder] > 0", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE [dbo].[Services]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Services]", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServiceController_NormalizesBlankUrlsAndHandlesConcurrentSlotConflicts()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(root, "Controllers/Servicecontroller.cs"));

        Assert.Contains("string.IsNullOrWhiteSpace(pageUrl) ? null : pageUrl.Trim()", source, StringComparison.Ordinal);
        Assert.True(
            source.Split("catch (DbUpdateException)", StringSplitOptions.None).Length - 1 >= 2,
            "Create and Update must both handle the database-enforced slot race.");
        Assert.Contains("HasActivePageSlotConflictAsync", source, StringComparison.Ordinal);
        Assert.Contains("return ActivePageSlotConflict();", source, StringComparison.Ordinal);
        Assert.Contains("throw;", source, StringComparison.Ordinal);
    }
}
