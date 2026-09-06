using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906161000_AddAppointmentCreatedAtIndex")]
public partial class AddAppointmentCreatedAtIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some deployed databases can receive emergency/manual indexes before EF
        // catches up. Keep this migration idempotent so rollout is safe either way.
        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_AppointmentRequests_CreatedAt'
      AND [object_id] = OBJECT_ID(N'[dbo].[AppointmentRequests]')
)
BEGIN
    CREATE INDEX [IX_AppointmentRequests_CreatedAt]
        ON [dbo].[AppointmentRequests] ([CreatedAt]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_AppointmentRequests_CreatedAt'
      AND [object_id] = OBJECT_ID(N'[dbo].[AppointmentRequests]')
)
BEGIN
    DROP INDEX [IX_AppointmentRequests_CreatedAt]
        ON [dbo].[AppointmentRequests];
END;
""");
    }
}
