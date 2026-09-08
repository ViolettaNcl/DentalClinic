using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909002000_EnforceCrossRoleEmailUniqueness")]
public partial class EnforceCrossRoleEmailUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

-- Patient and administrator identities intentionally share one email namespace.
-- Refuse to guess which account should win if legacy data already violates that
-- invariant. Runtime writes are serialized before either role table is modified by
-- IdentityEmailGuard; SQL Server cannot express this cross-table unique constraint
-- declaratively without introducing a separate identity table.
IF EXISTS (
    SELECT 1
    FROM [dbo].[Patients] p
    INNER JOIN [dbo].[Admins] a ON a.[Email] = p.[Email])
    THROW 51030, 'Cross-role email uniqueness migration stopped: an email exists in both Patients and Admins.', 1;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preflight-only migration: no schema object is created or removed.
    }
}
