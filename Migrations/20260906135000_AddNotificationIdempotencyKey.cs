using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906135000_AddNotificationIdempotencyKey")]
public partial class AddNotificationIdempotencyKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing notifications predate durable maintenance keys and remain NULL.
        // The filtered unique index therefore protects only identified operations
        // without changing the semantics of ordinary patient notifications.
        // Keep the column addition and index creation in separate SQL batches: SQL
        // Server compiles a batch before execution, so referencing a column that is
        // added earlier in the same batch can still fail with "Invalid column name".
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Notifications', 'IdempotencyKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Notifications]
        ADD [IdempotencyKey] nvarchar(120) NULL;
END;
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Notifications_IdempotencyKey'
      AND [object_id] = OBJECT_ID(N'[dbo].[Notifications]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Notifications_IdempotencyKey]
        ON [dbo].[Notifications] ([IdempotencyKey])
        WHERE [IdempotencyKey] IS NOT NULL;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Notifications_IdempotencyKey'
      AND [object_id] = OBJECT_ID(N'[dbo].[Notifications]'))
BEGIN
    DROP INDEX [IX_Notifications_IdempotencyKey] ON [dbo].[Notifications];
END;
""");

        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Notifications', 'IdempotencyKey') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Notifications] DROP COLUMN [IdempotencyKey];
END;
""");
    }
}
