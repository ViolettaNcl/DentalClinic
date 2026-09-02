/*
  Stage 1 database hardening for Microsoft SQL Server.

  Run only after taking a verified backup. The script is transactional and
  deliberately stops if it finds an unknown or empty appointment status.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE dbo.AppointmentRequests
    SET [Status] = LOWER(LTRIM(RTRIM([Status])))
    WHERE LOWER(LTRIM(RTRIM([Status]))) IN
        (N'pending', N'confirmed', N'cancelled', N'completed');

    IF EXISTS
    (
        SELECT 1
        FROM dbo.AppointmentRequests
        WHERE [Status] IS NULL
           OR [Status] NOT IN (N'pending', N'confirmed', N'cancelled', N'completed')
    )
    BEGIN
        THROW 51000,
            'AppointmentRequests contains an empty or unknown status. Correct those rows before applying Stage 1.',
            1;
    END;

    ALTER TABLE dbo.AppointmentRequests
        ALTER COLUMN [Status] nvarchar(20) NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE [name] = N'CK_AppointmentRequests_Status'
          AND [parent_object_id] = OBJECT_ID(N'dbo.AppointmentRequests')
    )
    BEGIN
        ALTER TABLE dbo.AppointmentRequests WITH CHECK
            ADD CONSTRAINT CK_AppointmentRequests_Status
            CHECK ([Status] IN (N'pending', N'confirmed', N'cancelled', N'completed'));
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_AppointmentRequests_DoctorId_AppointmentDate_Status'
          AND [object_id] = OBJECT_ID(N'dbo.AppointmentRequests')
    )
    BEGIN
        CREATE INDEX IX_AppointmentRequests_DoctorId_AppointmentDate_Status
            ON dbo.AppointmentRequests (DoctorId, AppointmentDate, [Status]);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
