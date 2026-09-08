using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908211500_ConstrainChatLogDomains")]
public partial class ConstrainChatLogDomains : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

-- Chat write paths normalize these values today. Abort instead of rewriting
-- legacy rows if production contains anything outside the documented domains.
IF EXISTS (SELECT 1 FROM [dbo].[ChatMessageLogs] WHERE [Role] NOT IN ('user', 'bot'))
    THROW 51030, 'Chat log domain migration stopped: unsupported role exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[ChatMessageLogs] WHERE [Lang] NOT IN ('ru', 'en', 'fr', 'el', 'ar'))
    THROW 51031, 'Chat log domain migration stopped: unsupported language exists.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]')
      AND name = N'CK_ChatMessageLogs_Role')
    ALTER TABLE [dbo].[ChatMessageLogs] WITH CHECK ADD CONSTRAINT [CK_ChatMessageLogs_Role]
        CHECK ([Role] IN ('user', 'bot'));

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]')
      AND name = N'CK_ChatMessageLogs_Lang')
    ALTER TABLE [dbo].[ChatMessageLogs] WITH CHECK ADD CONSTRAINT [CK_ChatMessageLogs_Lang]
        CHECK ([Lang] IN ('ru', 'en', 'fr', 'el', 'ar'));
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]') AND name = N'CK_ChatMessageLogs_Role')
    ALTER TABLE [dbo].[ChatMessageLogs] DROP CONSTRAINT [CK_ChatMessageLogs_Role];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]') AND name = N'CK_ChatMessageLogs_Lang')
    ALTER TABLE [dbo].[ChatMessageLogs] DROP CONSTRAINT [CK_ChatMessageLogs_Lang];
""");
    }
}
