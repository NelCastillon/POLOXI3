SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI Personal Injury semantic guardrails.
-- These rows define the permitted/reusable semantic universe, applicability, source routing and fallback
-- coverage. They are NOT an execution hierarchy. The LLM still proposes decision-specific candidates and
-- branches; POLOXI validates/enriches those proposals and persists the generated hierarchy separately.

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainConcept
(
	DecisionDomainConceptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainConcept PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainConcept_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	ConceptCode NVARCHAR(80) NOT NULL,
	DimensionCode NVARCHAR(60) NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	Description NVARCHAR(1200) NULL,
	ConceptKindCode NVARCHAR(40) NOT NULL,
	SourceClassCode NVARCHAR(40) NOT NULL,
	VerificationProfileCode NVARCHAR(60) NULL,
	JurisdictionCode NVARCHAR(120) NULL,
	MatterTypeCode NVARCHAR(120) NULL,
	IsRequiredCoverage BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Required DEFAULT 0,
	IsFallbackEligible BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Fallback DEFAULT 1,
	IsActive BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Active DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Sort DEFAULT 0,
	VersionNumber INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Version DEFAULT 1,
	TenantId UNIQUEIDENTIFIER NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConcept_Deleted DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'UX_Legal_DecisionDomainConcept_GlobalCode')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConcept_GlobalCode
		ON POLOXI.Legal_DecisionDomainConcept (DecisionDomainPackId, ConceptCode)
		WHERE TenantId IS NULL AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'UX_Legal_DecisionDomainConcept_TenantCode')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConcept_TenantCode
		ON POLOXI.Legal_DecisionDomainConcept (DecisionDomainPackId, TenantId, ConceptCode)
		WHERE TenantId IS NOT NULL AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'IX_Legal_DecisionDomainConcept_Applicable')
	CREATE INDEX IX_Legal_DecisionDomainConcept_Applicable
		ON POLOXI.Legal_DecisionDomainConcept (DecisionDomainPackId, IsActive, JurisdictionCode, MatterTypeCode, SortOrder)
		INCLUDE (ConceptCode, DimensionCode, ConceptKindCode, SourceClassCode, VerificationProfileCode, IsRequiredCoverage, IsFallbackEligible)
		WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainConceptRelation
(
	DecisionDomainConceptRelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainConceptRelation PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainConceptRelation_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	SourceDecisionDomainConceptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainConceptRelation_Source REFERENCES POLOXI.Legal_DecisionDomainConcept (DecisionDomainConceptId),
	TargetDecisionDomainConceptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainConceptRelation_Target REFERENCES POLOXI.Legal_DecisionDomainConcept (DecisionDomainConceptId),
	SourceConceptCode NVARCHAR(80) NOT NULL,
	TargetConceptCode NVARCHAR(80) NOT NULL,
	RelationTypeCode NVARCHAR(40) NOT NULL,
	ConstraintCode NVARCHAR(80) NULL,
	Description NVARCHAR(1200) NULL,
	JurisdictionCode NVARCHAR(120) NULL,
	MatterTypeCode NVARCHAR(120) NULL,
	IsHardConstraint BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConceptRelation_Hard DEFAULT 0,
	IsActive BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConceptRelation_Active DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConceptRelation_Sort DEFAULT 0,
	TenantId UNIQUEIDENTIFIER NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainConceptRelation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainConceptRelation_Deleted DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation') AND name = N'UX_Legal_DecisionDomainConceptRelation_GlobalEdge')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConceptRelation_GlobalEdge
		ON POLOXI.Legal_DecisionDomainConceptRelation (DecisionDomainPackId, SourceConceptCode, TargetConceptCode, RelationTypeCode)
		WHERE TenantId IS NULL AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation') AND name = N'UX_Legal_DecisionDomainConceptRelation_TenantEdge')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConceptRelation_TenantEdge
		ON POLOXI.Legal_DecisionDomainConceptRelation (DecisionDomainPackId, TenantId, SourceConceptCode, TargetConceptCode, RelationTypeCode)
		WHERE TenantId IS NOT NULL AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation') AND name = N'IX_Legal_DecisionDomainConceptRelation_Applicable')
	CREATE INDEX IX_Legal_DecisionDomainConceptRelation_Applicable
		ON POLOXI.Legal_DecisionDomainConceptRelation (DecisionDomainPackId, IsActive, JurisdictionCode, MatterTypeCode, SortOrder)
		INCLUDE (SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, IsHardConstraint)
		WHERE IsDeleted = 0;
GO

-- Generated execution branches retain their dynamic identity while recording which DB-backed guardrail,
-- if any, constrained or enriched them. NULL concept means a valid novel decision-specific branch.
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'GenerationOriginCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD GenerationOriginCode NVARCHAR(40) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'DecisionDomainConceptId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD DecisionDomainConceptId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'DomainConceptCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD DomainConceptCode NVARCHAR(80) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'GuardrailMatchScore') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD GuardrailMatchScore DECIMAL(5,4) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'GuardrailActionCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD GuardrailActionCode NVARCHAR(40) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionBranch', N'GuardrailVersion') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD GuardrailVersion INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_DecisionBranch_DomainConcept')
	ALTER TABLE POLOXI.Legal_DecisionBranch ADD CONSTRAINT FK_Legal_DecisionBranch_DomainConcept
		FOREIGN KEY (DecisionDomainConceptId) REFERENCES POLOXI.Legal_DecisionDomainConcept (DecisionDomainConceptId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionBranch') AND name = N'IX_Legal_DecisionBranch_DomainConcept')
	CREATE INDEX IX_Legal_DecisionBranch_DomainConcept
		ON POLOXI.Legal_DecisionBranch (DecisionDomainConceptId, DecisionSessionId)
		INCLUDE (DomainConceptCode, GenerationOriginCode, GuardrailActionCode, GuardrailMatchScore, GuardrailVersion)
		WHERE IsDeleted = 0 AND DecisionDomainConceptId IS NOT NULL;
GO

DECLARE @PiPackId UNIQUEIDENTIFIER =
(
	SELECT TOP 1 DecisionDomainPackId
	FROM POLOXI.Legal_DecisionDomainPack
	WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder
);

IF @PiPackId IS NULL
	THROW 50001, 'PERSONAL_INJURY domain pack must exist before migration 0289.', 1;

MERGE POLOXI.Legal_DecisionDomainConcept AS target
USING (VALUES
	(N'LIABILITY_DUTY_BREACH', N'LIABILITY', N'Duty and breach', N'Whether an actor owed and breached an applicable duty.', N'LEGAL_ISSUE', N'LEGAL_AUTHORITY', N'LIABILITY_FACTS', NULL, NULL, 1, 1, 10),
	(N'CAUSAL_NEGLIGENCE', N'CAUSATION', N'Causal negligence', N'Whether alleged negligent conduct was a factual and legal cause of the incident or injury.', N'MIXED', N'MATTER_DOCUMENT', N'LIABILITY_FACTS', NULL, NULL, 1, 1, 20),
	(N'COMPARATIVE_FAULT', N'DEFENSES', N'Comparative fault', N'Allocation of legally attributable causal fault among relevant actors.', N'MIXED', N'LEGAL_AUTHORITY', N'LIABILITY_FACTS', NULL, NULL, 1, 1, 30),
	(N'COMPARATIVE_FAULT_THRESHOLD', N'DEFENSES', N'Comparative-fault threshold', N'The governing jurisdiction''s threshold and legal consequence for an attributed share of fault.', N'LEGAL_RULE', N'LEGAL_AUTHORITY', N'LIABILITY_FACTS', NULL, NULL, 0, 1, 40),
	(N'DE_COMPARATIVE_FAULT_THRESHOLD', N'DEFENSES', N'Delaware comparative-negligence threshold', N'Delaware-specific statutory comparison threshold under 10 Del. C. § 8132; applicability must come from authoritative matter jurisdiction.', N'LEGAL_RULE', N'LEGAL_AUTHORITY', N'LIABILITY_FACTS', N'DELAWARE', NULL, 0, 1, 41),
	(N'FAULT_ALLOCATION_EVIDENCE', N'EVIDENCE', N'Fault-allocation evidence', N'Matter evidence capable of establishing conduct, causation, and relative responsibility.', N'MATTER_EVIDENCE', N'MATTER_DOCUMENT', N'LIABILITY_FACTS', NULL, NULL, 0, 1, 50),
	(N'INJURY_CAUSATION', N'CAUSATION', N'Injury causation', N'Whether the incident caused or aggravated the claimed injury.', N'MIXED', N'MATTER_DOCUMENT', N'MEDICAL_CAUSATION', NULL, NULL, 1, 1, 60),
	(N'DAMAGES_QUANTUM', N'DAMAGES', N'Damages quantum', N'The existence, amount, and legal recoverability of claimed damages.', N'MIXED', N'MATTER_DOCUMENT', N'DAMAGES_QUANTUM', NULL, NULL, 1, 1, 70),
	(N'INSURANCE_COVERAGE_LIMITS', N'INSURANCE', N'Coverage and limits', N'Applicable coverage, exclusions, limits, and verified policy information.', N'MIXED', N'MATTER_DOCUMENT', NULL, NULL, NULL, 0, 1, 80),
	(N'PROCEDURAL_POSTURE', N'PROCEDURE', N'Procedural posture and available remedy', N'Whether the requested disposition is procedurally available on the established record.', N'PROCEDURAL_STANDARD', N'LEGAL_AUTHORITY', NULL, NULL, NULL, 1, 1, 90),
	(N'SETTLEMENT_STATUS', N'SETTLEMENT', N'Settlement status and enforceability', N'Whether an agreement exists and what approval or enforcement path applies.', N'MIXED', N'MATTER_DOCUMENT', NULL, NULL, NULL, 0, 1, 100),
	(N'BURDEN_STANDARD', N'PROCEDURE', N'Burden and decision standard', N'The governing burden, standard, and record sufficiency for the requested decision.', N'PROCEDURAL_STANDARD', N'LEGAL_AUTHORITY', NULL, NULL, NULL, 1, 1, 110)
) AS source (ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.ConceptCode = source.ConceptCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	DimensionCode = source.DimensionCode, Name = source.Name, Description = source.Description,
	ConceptKindCode = source.ConceptKindCode, SourceClassCode = source.SourceClassCode,
	VerificationProfileCode = source.VerificationProfileCode, JurisdictionCode = source.JurisdictionCode,
	MatterTypeCode = source.MatterTypeCode, IsRequiredCoverage = source.IsRequiredCoverage,
	IsFallbackEligible = source.IsFallbackEligible, SortOrder = source.SortOrder, IsActive = 1,
	IsDeleted = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder, TenantId)
	VALUES (@PiPackId, source.ConceptCode, source.DimensionCode, source.Name, source.Description, source.ConceptKindCode, source.SourceClassCode, source.VerificationProfileCode, source.JurisdictionCode, source.MatterTypeCode, source.IsRequiredCoverage, source.IsFallbackEligible, source.SortOrder, NULL);

MERGE POLOXI.Legal_DecisionDomainConceptRelation AS target
USING
(
	SELECT sourceConcept.DecisionDomainConceptId AS SourceDecisionDomainConceptId,
		targetConcept.DecisionDomainConceptId AS TargetDecisionDomainConceptId, relation.*
	FROM (VALUES
	(N'COMPARATIVE_FAULT', N'COMPARATIVE_FAULT_THRESHOLD', N'DECOMPOSES_TO', N'ATOMIC_RULE_AND_FACT', N'Decompose comparative fault into governing threshold, matter allocation facts/evidence, and deferred application.', NULL, NULL, 1, 10),
	(N'COMPARATIVE_FAULT', N'FAULT_ALLOCATION_EVIDENCE', N'REQUIRES', N'MATTER_EVIDENCE_REQUIRED', N'Comparative-fault application requires matter evidence establishing attributable causal conduct.', NULL, NULL, 1, 20),
	(N'DE_COMPARATIVE_FAULT_THRESHOLD', N'COMPARATIVE_FAULT', N'SPECIALIZES', N'JURISDICTION_APPLICABILITY', N'Use only when authoritative matter jurisdiction or governing law is Delaware.', N'DELAWARE', NULL, 1, 30),
	(N'CAUSAL_NEGLIGENCE', N'FAULT_ALLOCATION_EVIDENCE', N'REQUIRES', N'CAUSATION_EVIDENCE_REQUIRED', N'A negligence allegation cannot establish allocation without evidence of causal contribution.', NULL, NULL, 1, 40),
	(N'INJURY_CAUSATION', N'FAULT_ALLOCATION_EVIDENCE', N'DISTINCT_FROM', N'LIABILITY_INJURY_CAUSATION_SEPARATION', N'Liability causation and medical injury causation must remain semantically distinct.', NULL, NULL, 0, 50),
	(N'PROCEDURAL_POSTURE', N'BURDEN_STANDARD', N'REQUIRES', N'PROCEDURAL_STANDARD_REQUIRED', N'Procedural disposition must identify the governing burden and decision standard.', NULL, NULL, 1, 60),
	(N'SETTLEMENT_STATUS', N'PROCEDURAL_POSTURE', N'MAY_ACTIVATE', N'SETTLEMENT_REMEDY_PATH', N'An established settlement may activate enforcement or approval analysis rather than merits adjudication.', NULL, NULL, 0, 70)
	) AS relation (SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder)
	INNER JOIN POLOXI.Legal_DecisionDomainConcept sourceConcept
		ON sourceConcept.DecisionDomainPackId = @PiPackId AND sourceConcept.ConceptCode = relation.SourceConceptCode
		AND sourceConcept.TenantId IS NULL AND sourceConcept.IsDeleted = 0
	INNER JOIN POLOXI.Legal_DecisionDomainConcept targetConcept
		ON targetConcept.DecisionDomainPackId = @PiPackId AND targetConcept.ConceptCode = relation.TargetConceptCode
		AND targetConcept.TenantId IS NULL AND targetConcept.IsDeleted = 0
) AS source
ON target.DecisionDomainPackId = @PiPackId
	AND target.SourceConceptCode = source.SourceConceptCode
	AND target.TargetConceptCode = source.TargetConceptCode
	AND target.RelationTypeCode = source.RelationTypeCode
	AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	SourceDecisionDomainConceptId = source.SourceDecisionDomainConceptId,
	TargetDecisionDomainConceptId = source.TargetDecisionDomainConceptId,
	ConstraintCode = source.ConstraintCode, Description = source.Description,
	JurisdictionCode = source.JurisdictionCode, MatterTypeCode = source.MatterTypeCode,
	IsHardConstraint = source.IsHardConstraint, SortOrder = source.SortOrder, IsActive = 1, IsDeleted = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, SourceDecisionDomainConceptId, TargetDecisionDomainConceptId, SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder, TenantId)
	VALUES (@PiPackId, source.SourceDecisionDomainConceptId, source.TargetDecisionDomainConceptId, source.SourceConceptCode, source.TargetConceptCode, source.RelationTypeCode, source.ConstraintCode, source.Description, source.JurisdictionCode, source.MatterTypeCode, source.IsHardConstraint, source.SortOrder, NULL);

COMMIT TRANSACTION;
