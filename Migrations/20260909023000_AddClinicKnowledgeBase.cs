using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909023000_AddClinicKnowledgeBase")]
public partial class AddClinicKnowledgeBase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ClinicKnowledgeItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClinicKnowledgeItems] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(80) NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Content] nvarchar(1200) NOT NULL,
        [Keywords] nvarchar(300) NULL,
        [SortOrder] int NOT NULL CONSTRAINT [DF_ClinicKnowledgeItems_SortOrder] DEFAULT 0,
        [IsActive] bit NOT NULL CONSTRAINT [DF_ClinicKnowledgeItems_IsActive] DEFAULT 1,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ClinicKnowledgeItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ClinicKnowledgeItems_SortOrder] CHECK ([SortOrder] >= 0)
    );

    CREATE INDEX [IX_ClinicKnowledgeItems_ActiveSort]
        ON [dbo].[ClinicKnowledgeItems] ([IsActive], [SortOrder]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ClinicKnowledgeItems]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ClinicKnowledgeItems];
""");
    }
}
