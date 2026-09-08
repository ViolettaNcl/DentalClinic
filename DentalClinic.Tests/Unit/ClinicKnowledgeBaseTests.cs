using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Migrations;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ClinicKnowledgeBaseTests
{
    [Fact]
    public async Task DentaKnowledge_UsesOnlyActiveItemsAndRespectsConfiguredLimit()
    {
        await using var db = CreateContext();
        db.ClinicKnowledgeItems.AddRange(
            new ClinicKnowledgeItem
            {
                Category = "payment",
                Title = "First|item",
                Content = "Line one\nLine two",
                Keywords = "card cash",
                SortOrder = 1,
                IsActive = true
            },
            new ClinicKnowledgeItem
            {
                Category = "booking",
                Title = "Second item",
                Content = "Should be outside configured limit",
                SortOrder = 2,
                IsActive = true
            },
            new ClinicKnowledgeItem
            {
                Category = "legacy",
                Title = "Inactive item",
                Content = "Must never reach Denta",
                SortOrder = 0,
                IsActive = false
            });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChatKnowledge:MaxItems"] = "1"
            })
            .Build();

        var service = new ChatKnowledgeService(db, config);
        var block = await service.GetKnowledgeBlockAsync();

        Assert.Contains("=== MANAGED_CLINIC_KNOWLEDGE ===", block, StringComparison.Ordinal);
        Assert.Contains("title=First/item", block, StringComparison.Ordinal);
        Assert.Contains("content=Line one Line two", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Second item", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Inactive item", block, StringComparison.Ordinal);
    }

    [Fact]
    public void EfModel_DeclaresKnowledgeIndexAndSortConstraint()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(ClinicKnowledgeItem))!;

        Assert.Contains(
            entity.GetIndexes(),
            index => string.Equals(index.GetDatabaseName(), "IX_ClinicKnowledgeItems_ActiveSort", StringComparison.Ordinal));
        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => string.Equals(constraint.Name, "CK_ClinicKnowledgeItems_SortOrder", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_CreatesBoundedKnowledgeTableAndIndex()
    {
        var migration = new TestableKnowledgeMigration();
        var sql = string.Join(
            "\n",
            migration.BuildUpOperations().OfType<SqlOperation>().Select(op => op.Sql));

        Assert.Contains("CREATE TABLE [dbo].[ClinicKnowledgeItems]", sql, StringComparison.Ordinal);
        Assert.Contains("nvarchar(1200)", sql, StringComparison.Ordinal);
        Assert.Contains("CK_ClinicKnowledgeItems_SortOrder", sql, StringComparison.Ordinal);
        Assert.Contains("IX_ClinicKnowledgeItems_ActiveSort", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminApi_IsAdminOnly()
    {
        var authorize = typeof(ClinicKnowledgeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin", authorize.Roles);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"clinic-knowledge-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestableKnowledgeMigration : AddClinicKnowledgeBase
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }
    }
}
