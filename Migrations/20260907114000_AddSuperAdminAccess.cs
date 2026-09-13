using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907114000_AddSuperAdminAccess")]
public partial class AddSuperAdminAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Production migrations must not grant elevated privileges implicitly.
        // Add the capability flag only; a super-admin must be designated explicitly
        // through an intentional administrative/database operation after deployment.
        migrationBuilder.Sql("""
IF COL_LENGTH(N'[dbo].[Admins]', N'IsSuperAdmin') IS NULL
BEGIN
    ALTER TABLE [dbo].[Admins]
        ADD [IsSuperAdmin] bit NOT NULL
        CONSTRAINT [DF_Admins_IsSuperAdmin] DEFAULT(0);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'[dbo].[Admins]', N'IsSuperAdmin') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.[name]
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[default_object_id] = dc.[object_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[Admins]')
      AND c.[name] = N'IsSuperAdmin';

    IF @constraintName IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[Admins] DROP CONSTRAINT [' + @constraintName + N']');

    ALTER TABLE [dbo].[Admins] DROP COLUMN [IsSuperAdmin];
END;
""");
    }
}