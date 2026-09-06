using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906133800_AddAppointmentCreatedAtIndex")]
public partial class AddAppointmentCreatedAtIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some live environments predate the current EF baseline. Make the index
        // creation idempotent so applying this migration is safe even if an operator
        // already added the same production index manually.
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
