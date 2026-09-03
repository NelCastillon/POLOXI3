-- V3.17.1 Deliverable Synthesis indicators backfill.
-- Migration 0175 originally shipped the EnableDeliverableSynthesis gate. If 0175 was already applied
-- before the DB-backed indicator list was added, the migration ledger will not rerun it. This migration
-- idempotently seeds only the indicator configuration so grouped resolution-like ambiguity branches can
-- be recognized without hardcoded C# keywords.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @SettingKey NVARCHAR(200)=N'Intelligence.SearchWide.DeliverableSynthesisIndicators';
	DECLARE @SettingValue NVARCHAR(4000)=N'compute|calculate|adjudicate|determine|decide|resolve|payable|owed|payment|amount|eligibility|eligible|approval|approved|denial|deny|adjustment|reason|classification|verdict|determination|yes/no';
	DECLARE @Description NVARCHAR(500)=N'V3.17 Deliverable Synthesis: DB-backed indicators for recognizing resolution-like grouped ambiguity branches when the answer kind is misclassified. Pipe-delimited; used only as an eligibility signal and never as evidence.';

	MERGE Core.ConfigurationSetting AS target
	USING(VALUES(@SettingKey,@SettingValue,N'DelimitedList',@Description)) AS source(SettingKey,SettingValue,DataTypeCode,Description)
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
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,
			source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
