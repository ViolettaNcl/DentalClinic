using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906181000_AddPaidApiUsageWindows")]
public partial class AddPaidApiUsageWindows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaidApiUsageWindows]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaidApiUsageWindows]
    (
        [Bucket] nvarchar(32) NOT NULL,
        [ClientKey] nvarchar(64) NOT NULL,
        [WindowStartUtc] datetime2 NOT NULL,
        [RequestCount] int NOT NULL,
        CONSTRAINT [PK_PaidApiUsageWindows] PRIMARY KEY ([Bucket], [ClientKey]),
        CONSTRAINT [CK_PaidApiUsageWindows_RequestCount] CHECK ([RequestCount] >= 0)
    );
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaidApiUsageWindows]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PaidApiUsageWindows];
""");
    }
}