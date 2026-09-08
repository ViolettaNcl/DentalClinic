using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909012000_EnforceActiveServicePageSlotUniqueness")]
public partial class EnforceActiveServicePageSlotUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

-- Active catalogue cards use (PageUrl, SortOrder) as a unique display slot.
-- Abort instead of silently changing legacy rows if production already contains
-- duplicate active slots that require operator review.
IF EXISTS (
    SELECT 1
    FROM [dbo].[Services]
    WHERE [IsActive] = 1
      AND [PageUrl] IS NOT NULL
      AND [SortOrder] > 0
    GROUP BY [PageUrl], [SortOrder]
    HAVING COUNT(*) > 1)
    THROW 51033, 'Service slot migration stopped: duplicate active PageUrl/SortOrder slots exist.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Services]')
      AND name = N'UX_Services_ActivePageSlot')
    CREATE UNIQUE INDEX [UX_Services_ActivePageSlot]
        ON [dbo].[Services] ([PageUrl], [SortOrder])
        WHERE [IsActive] = 1 AND [PageUrl] IS NOT NULL AND [SortOrder] > 0;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Services]')
      AND name = N'UX_Services_ActivePageSlot')
    DROP INDEX [UX_Services_ActivePageSlot] ON [dbo].[Services];
""");
    }
}
