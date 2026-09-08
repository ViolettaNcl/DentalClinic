using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909005000_ConstrainNotificationTypes")]
public partial class ConstrainNotificationTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

-- Durable notification producers use a closed domain. Abort instead of
-- rewriting legacy rows if production contains an unknown value.
IF EXISTS (
    SELECT 1
    FROM [dbo].[Notifications]
    WHERE [Type] IS NULL
       OR [Type] NOT IN (
            'welcome',
            'appointment_confirmed',
            'appointment_cancelled',
            'appointment_completed',
            'appointment_reminder',
            'appointment_followup',
            'review_approved',
            'review_rejected'))
    THROW 51032, 'Notification type migration stopped: unsupported durable notification type exists.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Notifications]')
      AND name = N'CK_Notifications_Type')
    ALTER TABLE [dbo].[Notifications] WITH CHECK ADD CONSTRAINT [CK_Notifications_Type]
        CHECK ([Type] IN (
            'welcome',
            'appointment_confirmed',
            'appointment_cancelled',
            'appointment_completed',
            'appointment_reminder',
            'appointment_followup',
            'review_approved',
            'review_rejected'));
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Notifications]')
      AND name = N'CK_Notifications_Type')
    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [CK_Notifications_Type];
""");
    }
}
