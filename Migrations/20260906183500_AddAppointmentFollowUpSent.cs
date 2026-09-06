using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906183500_AddAppointmentFollowUpSent")]
public partial class AddAppointmentFollowUpSent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.AppointmentRequests', 'FollowUpSent') IS NULL
BEGIN
    ALTER TABLE [dbo].[AppointmentRequests]
        ADD [FollowUpSent] bit NOT NULL
            CONSTRAINT [DF_AppointmentRequests_FollowUpSent] DEFAULT CAST(0 AS bit);
END;

-- Preserve delivery history that predates the appointment-level marker. Once a
-- patient has already received a follow-up, deleting the Notification row later
-- must not make that appointment eligible again.
UPDATE request
SET [FollowUpSent] = CAST(1 AS bit)
FROM [dbo].[AppointmentRequests] AS request
WHERE [FollowUpSent] = CAST(0 AS bit)
  AND EXISTS (
      SELECT 1
      FROM [dbo].[Notifications] AS notification
      WHERE notification.[PatientId] = request.[PatientId]
        AND notification.[Type] = N'appointment_followup'
        AND notification.[RelatedId] = request.[Id]
  );
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.AppointmentRequests', 'FollowUpSent') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.[name]
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c
        ON c.[default_object_id] = dc.[object_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[AppointmentRequests]')
      AND c.[name] = N'FollowUpSent';

    IF @constraintName IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[AppointmentRequests] DROP CONSTRAINT [' + @constraintName + N']');

    ALTER TABLE [dbo].[AppointmentRequests] DROP COLUMN [FollowUpSent];
END;
""");
    }
}
