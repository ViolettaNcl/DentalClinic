using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909031000_AddClinicKnowledgeLocalizations")]
public partial class AddClinicKnowledgeLocalizations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ClinicKnowledgeLocalizations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClinicKnowledgeLocalizations] (
        [Id] int NOT NULL IDENTITY,
        [ClinicKnowledgeItemId] int NOT NULL,
        [Lang] nvarchar(2) NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Content] nvarchar(1200) NOT NULL,
        [Keywords] nvarchar(300) NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ClinicKnowledgeLocalizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClinicKnowledgeLocalizations_ClinicKnowledgeItems_ClinicKnowledgeItemId]
            FOREIGN KEY ([ClinicKnowledgeItemId]) REFERENCES [dbo].[ClinicKnowledgeItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_ClinicKnowledgeLocalizations_Lang]
            CHECK ([Lang] IN ('en', 'fr', 'el', 'ar'))
    );

    CREATE UNIQUE INDEX [UX_ClinicKnowledgeLocalizations_ItemLang]
        ON [dbo].[ClinicKnowledgeLocalizations] ([ClinicKnowledgeItemId], [Lang]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ClinicKnowledgeLocalizations]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ClinicKnowledgeLocalizations];
""");
    }
}
