using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906172500_AddDurableAvatarStorage")]
public partial class AddDurableAvatarStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'[dbo].[Patients]', N'AvatarData') IS NULL
BEGIN
    ALTER TABLE [dbo].[Patients] ADD [AvatarData] varbinary(max) NULL;
END;

IF COL_LENGTH(N'[dbo].[Patients]', N'AvatarContentType') IS NULL
BEGIN
    ALTER TABLE [dbo].[Patients] ADD [AvatarContentType] nvarchar(50) NULL;
END;

IF COL_LENGTH(N'[dbo].[Admins]', N'AvatarData') IS NULL
BEGIN
    ALTER TABLE [dbo].[Admins] ADD [AvatarData] varbinary(max) NULL;
END;

IF COL_LENGTH(N'[dbo].[Admins]', N'AvatarContentType') IS NULL
BEGIN
    ALTER TABLE [dbo].[Admins] ADD [AvatarContentType] nvarchar(50) NULL;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'[dbo].[Patients]', N'AvatarContentType') IS NOT NULL
    ALTER TABLE [dbo].[Patients] DROP COLUMN [AvatarContentType];
IF COL_LENGTH(N'[dbo].[Patients]', N'AvatarData') IS NOT NULL
    ALTER TABLE [dbo].[Patients] DROP COLUMN [AvatarData];
IF COL_LENGTH(N'[dbo].[Admins]', N'AvatarContentType') IS NOT NULL
    ALTER TABLE [dbo].[Admins] DROP COLUMN [AvatarContentType];
IF COL_LENGTH(N'[dbo].[Admins]', N'AvatarData') IS NOT NULL
    ALTER TABLE [dbo].[Admins] DROP COLUMN [AvatarData];
""");
    }
}