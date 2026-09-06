using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906104000_AddAuthenticationTokenVersion")]
public partial class AddAuthenticationTokenVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent SQL keeps this safe for the project's legacy/live database path
        // as well as databases that were created through the Stage2 baseline.
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Patients', 'TokenVersion') IS NULL
BEGIN
    ALTER TABLE [dbo].[Patients]
        ADD [TokenVersion] int NOT NULL
            CONSTRAINT [DF_Patients_TokenVersion] DEFAULT (0);
END;

IF COL_LENGTH('dbo.Admins', 'TokenVersion') IS NULL
BEGIN
    ALTER TABLE [dbo].[Admins]
        ADD [TokenVersion] int NOT NULL
            CONSTRAINT [DF_Admins_TokenVersion] DEFAULT (0);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Patients', 'TokenVersion') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[DF_Patients_TokenVersion]', N'D') IS NOT NULL
        ALTER TABLE [dbo].[Patients] DROP CONSTRAINT [DF_Patients_TokenVersion];
    ALTER TABLE [dbo].[Patients] DROP COLUMN [TokenVersion];
END;

IF COL_LENGTH('dbo.Admins', 'TokenVersion') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[DF_Admins_TokenVersion]', N'D') IS NOT NULL
        ALTER TABLE [dbo].[Admins] DROP CONSTRAINT [DF_Admins_TokenVersion];
    ALTER TABLE [dbo].[Admins] DROP COLUMN [TokenVersion];
END;
""");
    }
}
