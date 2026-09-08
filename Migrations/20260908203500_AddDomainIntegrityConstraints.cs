using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908203500_AddDomainIntegrityConstraints")]
public partial class AddDomainIntegrityConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

-- Do not silently rewrite legacy production data. If an invariant is already
-- violated, stop the migration so an operator can inspect and correct the row.
IF EXISTS (SELECT 1 FROM [dbo].[Reviews] WHERE [Rating] < 1 OR [Rating] > 5)
    THROW 51020, 'Domain integrity migration stopped: review rating outside 1..5 exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Reviews] WHERE [Status] NOT IN ('pending', 'approved', 'rejected'))
    THROW 51021, 'Domain integrity migration stopped: unsupported review status exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Doctors] WHERE [ExperienceYears] IS NOT NULL AND ([ExperienceYears] < 0 OR [ExperienceYears] > 80))
    THROW 51022, 'Domain integrity migration stopped: doctor experience outside 0..80 exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Services] WHERE [PriceFrom] < 0 OR ([PriceTo] IS NOT NULL AND [PriceTo] < [PriceFrom]))
    THROW 51023, 'Domain integrity migration stopped: invalid service price range exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Services] WHERE [SortOrder] < 0)
    THROW 51024, 'Domain integrity migration stopped: negative service sort order exists.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Reviews]')
      AND name = N'CK_Reviews_Rating')
    ALTER TABLE [dbo].[Reviews] WITH CHECK ADD CONSTRAINT [CK_Reviews_Rating]
        CHECK ([Rating] BETWEEN 1 AND 5);

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Reviews]')
      AND name = N'CK_Reviews_Status')
    ALTER TABLE [dbo].[Reviews] WITH CHECK ADD CONSTRAINT [CK_Reviews_Status]
        CHECK ([Status] IN ('pending', 'approved', 'rejected'));

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Doctors]')
      AND name = N'CK_Doctors_ExperienceYears')
    ALTER TABLE [dbo].[Doctors] WITH CHECK ADD CONSTRAINT [CK_Doctors_ExperienceYears]
        CHECK ([ExperienceYears] IS NULL OR [ExperienceYears] BETWEEN 0 AND 80);

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Services]')
      AND name = N'CK_Services_PriceRange')
    ALTER TABLE [dbo].[Services] WITH CHECK ADD CONSTRAINT [CK_Services_PriceRange]
        CHECK ([PriceFrom] >= 0 AND ([PriceTo] IS NULL OR [PriceTo] >= [PriceFrom]));

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Services]')
      AND name = N'CK_Services_SortOrder')
    ALTER TABLE [dbo].[Services] WITH CHECK ADD CONSTRAINT [CK_Services_SortOrder]
        CHECK ([SortOrder] >= 0);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Reviews]') AND name = N'CK_Reviews_Rating')
    ALTER TABLE [dbo].[Reviews] DROP CONSTRAINT [CK_Reviews_Rating];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Reviews]') AND name = N'CK_Reviews_Status')
    ALTER TABLE [dbo].[Reviews] DROP CONSTRAINT [CK_Reviews_Status];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Doctors]') AND name = N'CK_Doctors_ExperienceYears')
    ALTER TABLE [dbo].[Doctors] DROP CONSTRAINT [CK_Doctors_ExperienceYears];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Services]') AND name = N'CK_Services_PriceRange')
    ALTER TABLE [dbo].[Services] DROP CONSTRAINT [CK_Services_PriceRange];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Services]') AND name = N'CK_Services_SortOrder')
    ALTER TABLE [dbo].[Services] DROP CONSTRAINT [CK_Services_SortOrder];
""");
    }
}
