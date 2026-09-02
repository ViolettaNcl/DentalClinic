using DentalClinic.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902080000_Stage2BaselineAndHardening")]
public partial class Stage2BaselineAndHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This project predates EF migration history and already has a live SQL Server
        // database. The first migration is deliberately idempotent: it creates missing
        // tables for a fresh database and hardens an existing legacy database in place.
        migrationBuilder.Sql("""
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[Patients]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Patients] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Patients] PRIMARY KEY,
        [FirstName] nvarchar(max) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [Phone] nvarchar(max) NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [AvatarUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL
    );
END;

IF OBJECT_ID(N'[dbo].[Admins]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Admins] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Admins] PRIMARY KEY,
        [Email] nvarchar(320) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [AvatarUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL
    );
END;

IF OBJECT_ID(N'[dbo].[Doctors]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Doctors] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Doctors] PRIMARY KEY,
        [FullName] nvarchar(150) NOT NULL,
        [FullNameEn] nvarchar(150) NULL,
        [FullNameFr] nvarchar(150) NULL,
        [FullNameEl] nvarchar(150) NULL,
        [FullNameAr] nvarchar(150) NULL,
        [Specialization] nvarchar(300) NULL,
        [ExperienceYears] int NULL,
        [Bio] nvarchar(500) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Doctors_IsActive] DEFAULT (1)
    );
END;

IF OBJECT_ID(N'[dbo].[Services]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Services] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Services] PRIMARY KEY,
        [Category] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [PriceFrom] decimal(10,2) NOT NULL,
        [PriceTo] decimal(10,2) NULL,
        [Unit] nvarchar(30) NULL,
        [Keywords] nvarchar(300) NULL,
        [PageUrl] nvarchar(300) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Services_IsActive] DEFAULT (1),
        [SortOrder] int NOT NULL CONSTRAINT [DF_Services_SortOrder] DEFAULT (0)
    );
END;

IF OBJECT_ID(N'[dbo].[AppointmentRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppointmentRequests] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AppointmentRequests] PRIMARY KEY,
        [PatientId] int NULL,
        [FirstName] nvarchar(100) NULL,
        [Phone] nvarchar(20) NOT NULL,
        [AppointmentDate] datetime2 NULL,
        [Comment] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [DoctorId] int NULL,
        [ReminderSent] bit NOT NULL CONSTRAINT [DF_AppointmentRequests_ReminderSent] DEFAULT (0)
    );
END;

IF OBJECT_ID(N'[dbo].[Reviews]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Reviews] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Reviews] PRIMARY KEY,
        [PatientId] int NOT NULL,
        [Rating] int NOT NULL,
        [Text] nvarchar(1000) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ModeratedAt] datetime2 NULL,
        [IsNotificationRead] bit NOT NULL CONSTRAINT [DF_Reviews_IsNotificationRead] DEFAULT (0)
    );
END;

IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Notifications] PRIMARY KEY,
        [PatientId] int NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Message] nvarchar(550) NOT NULL,
        [RelatedId] int NULL,
        [IsRead] bit NOT NULL CONSTRAINT [DF_Notifications_IsRead] DEFAULT (0),
        [CreatedAt] datetime2 NOT NULL
    );
END;

IF OBJECT_ID(N'[dbo].[ChatMessageLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessageLogs] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatMessageLogs] PRIMARY KEY,
        [SessionId] nvarchar(64) NOT NULL,
        [PatientId] int NULL,
        [Role] nvarchar(10) NOT NULL,
        [Text] nvarchar(1000) NOT NULL,
        [Lang] nvarchar(5) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ClientIp] nvarchar(64) NULL
    );
END;

-- Normalize email before enforcing database-level uniqueness.
UPDATE [dbo].[Patients] SET [Email] = LOWER(LTRIM(RTRIM([Email]))) WHERE [Email] IS NOT NULL;
UPDATE [dbo].[Admins] SET [Email] = LOWER(LTRIM(RTRIM([Email]))) WHERE [Email] IS NOT NULL;

IF EXISTS (SELECT 1 FROM [dbo].[Patients] GROUP BY [Email] HAVING COUNT(*) > 1)
    THROW 51001, 'Stage2 migration stopped: duplicate patient email exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Admins] GROUP BY [Email] HAVING COUNT(*) > 1)
    THROW 51002, 'Stage2 migration stopped: duplicate admin email exists.', 1;

ALTER TABLE [dbo].[Patients] ALTER COLUMN [Email] nvarchar(320) NOT NULL;
ALTER TABLE [dbo].[Admins] ALTER COLUMN [Email] nvarchar(320) NOT NULL;
IF COL_LENGTH('dbo.Reviews', 'Status') IS NOT NULL
    ALTER TABLE [dbo].[Reviews] ALTER COLUMN [Status] nvarchar(40) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Patients]') AND name = N'IX_Patients_Email')
    CREATE UNIQUE INDEX [IX_Patients_Email] ON [dbo].[Patients]([Email]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Admins]') AND name = N'IX_Admins_Email')
    CREATE UNIQUE INDEX [IX_Admins_Email] ON [dbo].[Admins]([Email]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Reviews]') AND name = N'IX_Reviews_Status')
    CREATE INDEX [IX_Reviews_Status] ON [dbo].[Reviews]([Status]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notifications]') AND name = N'IX_Notifications_PatientId_IsRead')
    CREATE INDEX [IX_Notifications_PatientId_IsRead] ON [dbo].[Notifications]([PatientId], [IsRead]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = N'IX_Services_Category_IsActive')
    CREATE INDEX [IX_Services_Category_IsActive] ON [dbo].[Services]([Category], [IsActive]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AppointmentRequests]') AND name = N'IX_AppointmentRequests_DoctorId_AppointmentDate_Status')
    CREATE INDEX [IX_AppointmentRequests_DoctorId_AppointmentDate_Status] ON [dbo].[AppointmentRequests]([DoctorId], [AppointmentDate], [Status]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]') AND name = N'IX_ChatMessageLogs_SessionId')
    CREATE INDEX [IX_ChatMessageLogs_SessionId] ON [dbo].[ChatMessageLogs]([SessionId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]') AND name = N'IX_ChatMessageLogs_CreatedAt')
    CREATE INDEX [IX_ChatMessageLogs_CreatedAt] ON [dbo].[ChatMessageLogs]([CreatedAt]);

-- Legacy orphaned optional references can safely be detached.
UPDATE a SET [PatientId] = NULL
FROM [dbo].[AppointmentRequests] a
LEFT JOIN [dbo].[Patients] p ON p.[Id] = a.[PatientId]
WHERE a.[PatientId] IS NOT NULL AND p.[Id] IS NULL;
UPDATE a SET [DoctorId] = NULL
FROM [dbo].[AppointmentRequests] a
LEFT JOIN [dbo].[Doctors] d ON d.[Id] = a.[DoctorId]
WHERE a.[DoctorId] IS NOT NULL AND d.[Id] IS NULL;
UPDATE c SET [PatientId] = NULL
FROM [dbo].[ChatMessageLogs] c
LEFT JOIN [dbo].[Patients] p ON p.[Id] = c.[PatientId]
WHERE c.[PatientId] IS NOT NULL AND p.[Id] IS NULL;

IF EXISTS (SELECT 1 FROM [dbo].[Reviews] r LEFT JOIN [dbo].[Patients] p ON p.[Id] = r.[PatientId] WHERE p.[Id] IS NULL)
    THROW 51003, 'Stage2 migration stopped: orphaned review exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[Notifications] n LEFT JOIN [dbo].[Patients] p ON p.[Id] = n.[PatientId] WHERE p.[Id] IS NULL)
    THROW 51004, 'Stage2 migration stopped: orphaned notification exists.', 1;

DECLARE @fk sysname;
SELECT TOP(1) @fk = fk.name FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[AppointmentRequests]') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'PatientId';
IF @fk IS NOT NULL EXEC(N'ALTER TABLE [dbo].[AppointmentRequests] DROP CONSTRAINT ' + QUOTENAME(@fk));
ALTER TABLE [dbo].[AppointmentRequests] ADD CONSTRAINT [FK_AppointmentRequests_Patients_PatientId]
    FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE SET NULL;

SET @fk = NULL;
SELECT TOP(1) @fk = fk.name FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[AppointmentRequests]') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'DoctorId';
IF @fk IS NOT NULL EXEC(N'ALTER TABLE [dbo].[AppointmentRequests] DROP CONSTRAINT ' + QUOTENAME(@fk));
ALTER TABLE [dbo].[AppointmentRequests] ADD CONSTRAINT [FK_AppointmentRequests_Doctors_DoctorId]
    FOREIGN KEY ([DoctorId]) REFERENCES [dbo].[Doctors]([Id]) ON DELETE SET NULL;

SET @fk = NULL;
SELECT TOP(1) @fk = fk.name FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Reviews]') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'PatientId';
IF @fk IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Reviews] DROP CONSTRAINT ' + QUOTENAME(@fk));
ALTER TABLE [dbo].[Reviews] ADD CONSTRAINT [FK_Reviews_Patients_PatientId]
    FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE;

SET @fk = NULL;
SELECT TOP(1) @fk = fk.name FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Notifications]') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'PatientId';
IF @fk IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT ' + QUOTENAME(@fk));
ALTER TABLE [dbo].[Notifications] ADD CONSTRAINT [FK_Notifications_Patients_PatientId]
    FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE;

SET @fk = NULL;
SELECT TOP(1) @fk = fk.name FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[ChatMessageLogs]') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'PatientId';
IF @fk IS NOT NULL EXEC(N'ALTER TABLE [dbo].[ChatMessageLogs] DROP CONSTRAINT ' + QUOTENAME(@fk));
ALTER TABLE [dbo].[ChatMessageLogs] ADD CONSTRAINT [FK_ChatMessageLogs_Patients_PatientId]
    FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[AppointmentRequests]') AND name = N'CK_AppointmentRequests_Status')
    ALTER TABLE [dbo].[AppointmentRequests] WITH CHECK ADD CONSTRAINT [CK_AppointmentRequests_Status]
        CHECK ([Status] IN ('pending', 'confirmed', 'cancelled', 'completed'));
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately non-destructive: this first migration also acts as the legacy
        // baseline. A rollback must never drop the pre-existing clinic tables/data.
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Patients]') AND name = N'IX_Patients_Email') DROP INDEX [IX_Patients_Email] ON [dbo].[Patients];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Admins]') AND name = N'IX_Admins_Email') DROP INDEX [IX_Admins_Email] ON [dbo].[Admins];
""");
    }
}
