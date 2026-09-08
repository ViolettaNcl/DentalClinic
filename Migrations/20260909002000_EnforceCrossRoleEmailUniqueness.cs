using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909002000_EnforceCrossRoleEmailUniqueness")]
public partial class EnforceCrossRoleEmailUniqueness : Migration
{
    private const string IdentityLockResource = "DentalClinic.IdentityEmail";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
SET XACT_ABORT ON;

-- Patient and administrator identities intentionally share one email namespace.
-- Refuse to guess which account should win if legacy data already violates that
-- invariant; an operator must inspect the rows before this migration can proceed.
IF EXISTS (
    SELECT 1
    FROM [dbo].[Patients] p
    INNER JOIN [dbo].[Admins] a ON a.[Email] = p.[Email])
    THROW 51030, 'Cross-role email uniqueness migration stopped: an email exists in both Patients and Admins.', 1;

-- SQL Server cannot express a unique constraint across two independent tables.
-- These triggers serialize both write paths with the same transaction-owned
-- application lock, then check the opposite table. The lock closes the race where
-- concurrent Patient/Admin inserts could otherwise both pass separate prechecks.
EXEC(N'
CREATE OR ALTER TRIGGER [dbo].[TR_Patients_EnforceCrossRoleEmailUniqueness]
ON [dbo].[Patients]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N''{IdentityLockResource}'',
        @LockMode = ''Exclusive'',
        @LockOwner = ''Transaction'',
        @LockTimeout = 5000;

    IF @lockResult < 0
        THROW 51031, ''Unable to acquire cross-role identity lock.'', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN [dbo].[Admins] a ON a.[Email] = i.[Email])
        THROW 51032, ''Email is already assigned to an administrator account.'', 1;
END;
');

EXEC(N'
CREATE OR ALTER TRIGGER [dbo].[TR_Admins_EnforceCrossRoleEmailUniqueness]
ON [dbo].[Admins]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N''{IdentityLockResource}'',
        @LockMode = ''Exclusive'',
        @LockOwner = ''Transaction'',
        @LockTimeout = 5000;

    IF @lockResult < 0
        THROW 51033, ''Unable to acquire cross-role identity lock.'', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN [dbo].[Patients] p ON p.[Email] = i.[Email])
        THROW 51034, ''Email is already assigned to a patient account.'', 1;
END;
');
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[TR_Patients_EnforceCrossRoleEmailUniqueness]', N'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_Patients_EnforceCrossRoleEmailUniqueness];

IF OBJECT_ID(N'[dbo].[TR_Admins_EnforceCrossRoleEmailUniqueness]', N'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_Admins_EnforceCrossRoleEmailUniqueness];
""");
    }
}
