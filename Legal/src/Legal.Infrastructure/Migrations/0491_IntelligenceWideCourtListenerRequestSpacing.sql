SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- CourtListener rate-limit mitigation: minimum spacing (milliseconds) enforced
-- process-wide between the START of consecutive CourtListener requests. Concurrent
-- branch grounding (GroundingConcurrency) otherwise bursts the strict anonymous
-- CourtListener rate limit and trips HTTP 429. This throttles only CourtListener
-- traffic; it does not change GroundingConcurrency or any other provider.
-- Default 1200ms.
-- ============================================================================

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.LegalGrounding.CourtListener.MinRequestSpacingMs',N'1200',N'Integer',N'Minimum spacing in milliseconds enforced process-wide between the start of consecutive CourtListener requests so concurrent branch grounding cannot burst the strict anonymous rate limit and trip HTTP 429. Throttles only CourtListener traffic; does not affect GroundingConcurrency or other providers. Default 1200.');

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=0,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,0,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
