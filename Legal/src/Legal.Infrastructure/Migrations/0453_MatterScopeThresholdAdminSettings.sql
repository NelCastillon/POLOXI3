SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0453: Matter evidence-scope threshold admin settings.
--   Surfaces the per-matter Evidence Type scope-gate dials on /legal/configuration so they can be
--   tuned without SQL. These keys already exist (seeded across 0441/0445/0448/0443), but this
--   migration guarantees ALL of them are present at Platform scope with consistent descriptions,
--   so the Matter Evidence Scope settings section always renders the full set.
--   Idempotent and additive. Nothing is deleted; existing tenant overrides are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.MatterScopeOutThreshold',N'0.05',N'Decimal',
	 N'Blended (deterministic + LLM) relevance below which a target is auto-marked OUT_OF_SCOPE for the matter. Required-coverage, document-present and PRIORITIZE-rule targets are never auto-excluded. USER decisions are never overwritten. Raising this value prunes more weakly-related Evidence Types.'),
	(N'Intelligence.SearchWide.MatterScopePendingReviewFloor',N'0.10',N'Decimal',
	 N'PENDING_REVIEW band: targets with no positive matter signal whose blended relevance lies between this floor and MatterScopeOutThreshold are marked PENDING_REVIEW (dormant, attorney-decidable) instead of OUT_OF_SCOPE.'),
	(N'Intelligence.SearchWide.MatterScopeZeroScoreCeiling',N'0.05',N'Decimal',
	 N'Explicit LLM-inapplicability ceiling: when the model scores a target at or below this value it is treated as not applicable to the matter (distinct from merely weak lexical overlap).'),
	(N'Intelligence.SearchWide.MatterScopeDecisivePassageScore',N'0.60',N'Decimal',
	 N'Single-passage exception to the passage floor: one mapped passage admits the target when its lexical mapping score is at or above this value.'),
	(N'Intelligence.SearchWide.MatterScopeMinimumMappedPassages',N'2',N'Integer',
	 N'Passage-admission floor: an Evidence Type/Concept is admitted on mapped document passages alone only when at least this many distinct passages map to it. A single passage admits only when it is decisive (see MatterScopeDecisivePassageScore).');

	-- Refresh platform-default descriptions for keys already present (values/overrides preserved on existing SettingValue; only the shared default + description are normalized).
	UPDATE c SET c.Description=s.Description, c.DataTypeCode=s.DataTypeCode, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	-- Insert any missing platform rows so the admin UI always lists the full set.
	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting c
						WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.SettingKey=s.SettingKey AND c.IsDeleted=0);
END;

COMMIT TRANSACTION;
