SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DocumentSearchProjectionOutbox', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_DocumentSearchProjectionOutbox', N'ProcessingStartedDateUtc') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DocumentSearchProjectionOutbox
		ADD ProcessingStartedDateUtc DATETIME2 NULL;
END;

COMMIT TRANSACTION;
