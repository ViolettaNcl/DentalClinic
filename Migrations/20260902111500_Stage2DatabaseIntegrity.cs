using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902111500_Stage2DatabaseIntegrity")]
public sealed class Stage2DatabaseIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This project predates EF migration history. The migration is intentionally
        // idempotent and only adds Stage 2 integrity rules to the existing Somee DB.
        migrationBuilder.Sql("""
            UPDATE [Patients] SET [Email] = LOWER(LTRIM(RTRIM([Email]))) WHERE [Email] IS NOT NULL;
            UPDATE [Admins] SET [Email] = LOWER(LTRIM(RTRIM([Email]))) WHERE [Email] IS NOT NULL;

            IF EXISTS (SELECT 1 FROM [Patients] GROUP BY [Email] HAVING COUNT(*) > 1)
                THROW 51001, 'Duplicate patient emails must be resolved before Stage2 migration.', 1;
            IF EXISTS (SELECT 1 FROM [Admins] GROUP BY [Email] HAVING COUNT(*) > 1)
                THROW 51002, 'Duplicate admin emails must be resolved before Stage2 migration.', 1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Patients_Email' AND object_id = OBJECT_ID('Patients'))
                CREATE UNIQUE INDEX [UX_Patients_Email] ON [Patients]([Email]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Admins_Email' AND object_id = OBJECT_ID('Admins'))
                CREATE UNIQUE INDEX [UX_Admins_Email] ON [Admins]([Email]);

            UPDATE a SET [PatientId] = NULL
            FROM [AppointmentRequests] a
            WHERE a.[PatientId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Patients] p WHERE p.[Id] = a.[PatientId]);

            UPDATE a SET [DoctorId] = NULL
            FROM [AppointmentRequests] a
            WHERE a.[DoctorId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Doctors] d WHERE d.[Id] = a.[DoctorId]);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentRequests_Patients_PatientId')
                ALTER TABLE [AppointmentRequests] ADD CONSTRAINT [FK_AppointmentRequests_Patients_PatientId]
                    FOREIGN KEY ([PatientId]) REFERENCES [Patients]([Id]) ON DELETE SET NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentRequests_Doctors_DoctorId')
                ALTER TABLE [AppointmentRequests] ADD CONSTRAINT [FK_AppointmentRequests_Doctors_DoctorId]
                    FOREIGN KEY ([DoctorId]) REFERENCES [Doctors]([Id]) ON DELETE SET NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppointmentRequests_PatientId' AND object_id = OBJECT_ID('AppointmentRequests'))
                CREATE INDEX [IX_AppointmentRequests_PatientId] ON [AppointmentRequests]([PatientId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppointmentRequests_DoctorId' AND object_id = OBJECT_ID('AppointmentRequests'))
                CREATE INDEX [IX_AppointmentRequests_DoctorId] ON [AppointmentRequests]([DoctorId]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentRequests_Doctors_DoctorId')
                ALTER TABLE [AppointmentRequests] DROP CONSTRAINT [FK_AppointmentRequests_Doctors_DoctorId];
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentRequests_Patients_PatientId')
                ALTER TABLE [AppointmentRequests] DROP CONSTRAINT [FK_AppointmentRequests_Patients_PatientId];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppointmentRequests_DoctorId' AND object_id = OBJECT_ID('AppointmentRequests'))
                DROP INDEX [IX_AppointmentRequests_DoctorId] ON [AppointmentRequests];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppointmentRequests_PatientId' AND object_id = OBJECT_ID('AppointmentRequests'))
                DROP INDEX [IX_AppointmentRequests_PatientId] ON [AppointmentRequests];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Admins_Email' AND object_id = OBJECT_ID('Admins'))
                DROP INDEX [UX_Admins_Email] ON [Admins];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Patients_Email' AND object_id = OBJECT_ID('Patients'))
                DROP INDEX [UX_Patients_Email] ON [Patients];
            """);
    }
}
