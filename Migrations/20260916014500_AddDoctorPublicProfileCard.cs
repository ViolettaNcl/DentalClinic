using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916014500_AddDoctorPublicProfileCard")]
public partial class AddDoctorPublicProfileCard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Doctors', 'RoleTitle') IS NULL ALTER TABLE [dbo].[Doctors] ADD [RoleTitle] nvarchar(300) NULL;
IF COL_LENGTH('dbo.Doctors', 'Education') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Education] nvarchar(1200) NULL;
IF COL_LENGTH('dbo.Doctors', 'Skills') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Skills] nvarchar(1200) NULL;
IF COL_LENGTH('dbo.Doctors', 'Philosophy') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Philosophy] nvarchar(500) NULL;
IF COL_LENGTH('dbo.Doctors', 'Stat2Value') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Stat2Value] nvarchar(40) NULL;
IF COL_LENGTH('dbo.Doctors', 'Stat2Label') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Stat2Label] nvarchar(80) NULL;
IF COL_LENGTH('dbo.Doctors', 'Stat3Value') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Stat3Value] nvarchar(40) NULL;
IF COL_LENGTH('dbo.Doctors', 'Stat3Label') IS NULL ALTER TABLE [dbo].[Doctors] ADD [Stat3Label] nvarchar(80) NULL;
IF COL_LENGTH('dbo.Doctors', 'PhotoUrl') IS NULL ALTER TABLE [dbo].[Doctors] ADD [PhotoUrl] nvarchar(350) NULL;
IF COL_LENGTH('dbo.Doctors', 'PhotoData') IS NULL ALTER TABLE [dbo].[Doctors] ADD [PhotoData] varbinary(max) NULL;
IF COL_LENGTH('dbo.Doctors', 'PhotoContentType') IS NULL ALTER TABLE [dbo].[Doctors] ADD [PhotoContentType] nvarchar(50) NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Non-destructive by design. Doctor profile content and uploaded photos are
        // clinic data; a rollback must not silently delete them.
    }
}
