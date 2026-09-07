-- Cornell Legal Information Institute (LII, law.cornell.edu) secondary-source retrieval settings.
-- Cornell LII is the authoritative free host for the Uniform Commercial Code (UCC), the U.S. Code,
-- and the CFR. It exposes no JSON search API, so the CornellLiiLegalRetriever adapter resolves
-- UCC/USC/CFR citations deterministically to canonical LII pages. These DB-backed settings mirror
-- the existing CourtListener/GovInfo/eCFR legal-grounding configuration (DB is the source of truth).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL,
		IsEncrypted BIT NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description,IsEncrypted)
	VALUES
		-- Cornell LII (UCC / U.S. Code / CFR secondary source)
		(N'Intelligence.SearchWide.LegalGrounding.CornellLii.Enabled',N'true',N'Boolean',N'Enables Cornell LII (UCC/U.S. Code/CFR) citation resolution for the Legal context.',0),
		(N'Intelligence.SearchWide.LegalGrounding.CornellLii.BaseUrl',N'https://www.law.cornell.edu',N'String',N'Cornell Legal Information Institute base URL.',0);

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
			target.IsEncrypted=source.IsEncrypted,
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
			source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
