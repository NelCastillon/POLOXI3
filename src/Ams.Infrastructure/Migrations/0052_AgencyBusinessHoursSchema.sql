-- ============================================================
-- MIGRATION 0052: AGENCY BUSINESS HOURS SCHEMA
-- Persists tenant business hours, holiday closures, and emergency closure state
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Core')
BEGIN
	EXEC('CREATE SCHEMA Core');
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Core') AND name = 'AgencyBusinessHours')
BEGIN
	CREATE TABLE Core.AgencyBusinessHours (
		BusinessHoursId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
		TenantId             UNIQUEIDENTIFIER NOT NULL,
		TimeZoneId           NVARCHAR(100)    NOT NULL DEFAULT 'Eastern Standard Time',
		WeeklyScheduleJson   NVARCHAR(MAX)    NOT NULL,
		HolidayClosuresJson  NVARCHAR(MAX)    NOT NULL,
		EmergencyClosing     BIT              NOT NULL DEFAULT 0,
		EmergencyMessage     NVARCHAR(1000)   NULL,
		CreatedDateUtc       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
		ModifiedDateUtc      DATETIME2        NULL,
		IsDeleted            BIT              NOT NULL DEFAULT 0
	);

	CREATE UNIQUE NONCLUSTERED INDEX UX_AgencyBusinessHours_TenantId
		ON Core.AgencyBusinessHours(TenantId)
		WHERE IsDeleted = 0;
END
GO
