SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0441: Per-matter Evidence Type scope (Table/API/UI). One row per Decision Matter x Domain Pack L2 target
--   (Evidence Type or Concept). Holds the deterministic + LLM relevance decision for THIS matter, the
--   resulting scope status, and attorney overrides. Wide2 L3 generation reads this table: only IN_SCOPE
--   targets receive atomic-proposition generation; OUT_OF_SCOPE targets are persisted as eliminated
--   (never deleted). USER decisions are authoritative and never overwritten by the system.
--   Targets are always validated against the Domain Pack catalog (TargetKindCode + TargetCode + PackCode).
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

-- ── Lookup: scope status ─────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceScopeStatus',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterEvidenceScopeStatus
	(
		MatterEvidenceScopeStatusId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterEvidenceScopeStatus PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		StatusCode NVARCHAR(40) NOT NULL,
		Name NVARCHAR(120) NOT NULL,
		Description NVARCHAR(600) NULL,
		ParticipatesInL3 BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeStatus_L3 DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeStatus_Sort DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeStatus_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeStatus_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeStatus_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterEvidenceScopeStatus_Code ON POLOXI.Legal_MatterEvidenceScopeStatus(StatusCode) WHERE IsDeleted=0 AND TenantId IS NULL;
END;

MERGE POLOXI.Legal_MatterEvidenceScopeStatus AS t
USING (VALUES
	(N'IN_SCOPE',      N'In scope',        N'Evidence type participates in L3 atomic-proposition generation for this matter.',1,10),
	(N'OUT_OF_SCOPE',  N'Out of scope',    N'Evidence type is held back from L3 generation for this matter. Row is kept for audit and can be re-included.',0,20),
	(N'PENDING_REVIEW',N'Pending review',  N'System could not decide confidently; participates in L3 until an attorney decides.',1,30)
) AS s(StatusCode,Name,Description,ParticipatesInL3,SortOrder)
ON t.StatusCode=s.StatusCode AND t.TenantId IS NULL AND t.IsDeleted=0
WHEN NOT MATCHED THEN INSERT(TenantId,StatusCode,Name,Description,ParticipatesInL3,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
	VALUES(NULL,s.StatusCode,s.Name,s.Description,s.ParticipatesInL3,s.SortOrder,1,@Now,0);

-- ── Lookup: decided-by ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceScopeDecider',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterEvidenceScopeDecider
	(
		MatterEvidenceScopeDeciderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterEvidenceScopeDecider PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		DeciderCode NVARCHAR(30) NOT NULL,
		Name NVARCHAR(120) NOT NULL,
		Description NVARCHAR(600) NULL,
		IsAuthoritative BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeDecider_Auth DEFAULT 0,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeDecider_Sort DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeDecider_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeDecider_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScopeDecider_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterEvidenceScopeDecider_Code ON POLOXI.Legal_MatterEvidenceScopeDecider(DeciderCode) WHERE IsDeleted=0 AND TenantId IS NULL;
END;

MERGE POLOXI.Legal_MatterEvidenceScopeDecider AS t
USING (VALUES
	(N'SYSTEM',N'System (deterministic)',N'Decided by deterministic matter signals: matter type, keywords, documents, rules, passages.',0,10),
	(N'LLM',   N'LLM review',            N'Decided by the catalog-bound LLM relevance review blended with deterministic signals.',0,20),
	(N'RULE',  N'Domain Pack rule',      N'Decided by an EXCLUDE/PRIORITIZE relevance rule configured on the Domain Pack.',0,30),
	(N'USER',  N'Attorney override',     N'Decided by a user. Authoritative: the system never overwrites a USER decision.',1,40)
) AS s(DeciderCode,Name,Description,IsAuthoritative,SortOrder)
ON t.DeciderCode=s.DeciderCode AND t.TenantId IS NULL AND t.IsDeleted=0
WHEN NOT MATCHED THEN INSERT(TenantId,DeciderCode,Name,Description,IsAuthoritative,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
	VALUES(NULL,s.DeciderCode,s.Name,s.Description,s.IsAuthoritative,s.SortOrder,1,@Now,0);

-- ── Per-matter scope ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceScope',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterEvidenceScope
	(
		MatterEvidenceScopeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterEvidenceScope PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterEvidenceScope_Matter REFERENCES POLOXI.Legal_DecisionMatter(DecisionMatterId),
		PackCode NVARCHAR(80) NOT NULL,
		TargetKindCode NVARCHAR(30) NOT NULL,        -- CONCEPT | EVIDENCE_TYPE
		TargetCode NVARCHAR(240) NOT NULL,
		DimensionCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(300) NOT NULL,
		ScopeStatusCode NVARCHAR(40) NOT NULL,       -- Legal_MatterEvidenceScopeStatus
		DecidedByCode NVARCHAR(30) NOT NULL,         -- Legal_MatterEvidenceScopeDecider
		DeterministicScore DECIMAL(5,4) NULL,
		LlmScore DECIMAL(5,4) NULL,
		BlendedScore DECIMAL(5,4) NULL,
		TierCode NVARCHAR(20) NULL,                  -- TIER_A | TIER_B
		RelevanceReasons NVARCHAR(2000) NULL,
		LlmReason NVARCHAR(400) NULL,
		IsRequiredCoverage BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_Required DEFAULT 0,
		IsPrioritized BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_Prioritized DEFAULT 0,
		DocumentPresent BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_DocPresent DEFAULT 0,
		MappedPassageCount INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_Passages DEFAULT 0,
		UserNote NVARCHAR(1000) NULL,
		LastEvaluatedExecutionId UNIQUEIDENTIFIER NULL,
		LastEvaluatedDateUtc DATETIME2 NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterEvidenceScope_Target ON POLOXI.Legal_MatterEvidenceScope(TenantId,DecisionMatterId,PackCode,TargetKindCode,TargetCode) WHERE IsDeleted=0;
	CREATE INDEX IX_Legal_MatterEvidenceScope_Matter ON POLOXI.Legal_MatterEvidenceScope(TenantId,DecisionMatterId,ScopeStatusCode) INCLUDE(TargetKindCode,TargetCode,DimensionCode,DecidedByCode,BlendedScore,TierCode) WHERE IsDeleted=0;
END;

-- ── Settings ─────────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL,SettingValue NVARCHAR(2000) NOT NULL,DataTypeCode NVARCHAR(50) NOT NULL,Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
	(N'Intelligence.SearchWide.EnableMatterEvidenceScopeGate',N'true',N'Boolean',N'When true, Wide2 L3 generation reads POLOXI.Legal_MatterEvidenceScope for the matter and sends only IN_SCOPE / PENDING_REVIEW targets to atomic-proposition generation; OUT_OF_SCOPE targets are persisted as eliminated (MATTER_SCOPE_OUT), never deleted. Bypassed automatically when the matter has no matter type and no documents.'),
	(N'Intelligence.SearchWide.MatterScopeOutThreshold',N'0.05',N'Decimal',N'Blended (deterministic + LLM) relevance below which a target is auto-marked OUT_OF_SCOPE for the matter. Required-coverage, document-present and PRIORITIZE-rule targets are never auto-excluded. USER decisions are never overwritten.');
	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,@Now,0);
END;

COMMIT TRANSACTION;
