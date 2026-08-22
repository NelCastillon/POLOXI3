SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V3.0: Evidence-Guided Adaptive Narrowing.
-- Narrow by default: each useful iteration should shrink the reasoning space
-- (branches), the candidate space, and unresolved uncertainty. Expansion is
-- permitted ONLY when newly grounded evidence demonstrates the current space
-- may be incomplete (discovery admission gate + per-round budget), after which
-- narrowing resumes. Every narrowing decision is deterministic and audited
-- with full provenance (subject, previous/new state, evidence-based reason).
-- Invariants: no candidate is eliminated solely because of missing evidence;
-- hard invalidation requires deterministic constraint evidence; resolved
-- branches may reopen when dependent evidence materially changes.
-- ============================================================================

-- ── POLOXI.WideNarrowingIteration: one row per narrowing evaluation (per information round) ──
IF OBJECT_ID(N'POLOXI.WideNarrowingIteration',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.WideNarrowingIteration
	(
		WideNarrowingIterationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PoloxiWideNarrowingIteration PRIMARY KEY,
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PoloxiWideNarrowingIteration_Execution REFERENCES POLOXI.WideExecution(WideExecutionId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RoundNumber INT NOT NULL,
		TrendCode NVARCHAR(20) NOT NULL,
		ActiveBranchCountBefore INT NOT NULL,
		ActiveBranchCountAfter INT NOT NULL,
		CandidateCountBefore INT NOT NULL,
		CandidateCountAfter INT NOT NULL,
		NormalizedEntropyBefore DECIMAL(5,4) NOT NULL,
		NormalizedEntropyAfter DECIMAL(5,4) NULL,
		ActualInformationGain DECIMAL(9,4) NULL,
		ResolvedBranchCount INT NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Resolved DEFAULT 0,
		ReopenedBranchCount INT NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Reopened DEFAULT 0,
		AdmittedCandidateCount INT NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Admitted DEFAULT 0,
		DiscoveredNotAdmittedCount INT NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_NotAdmitted DEFAULT 0,
		TransitionsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Transitions DEFAULT N'[]',
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PoloxiWideNarrowIter_Deleted DEFAULT 0
	);
	CREATE INDEX IX_PoloxiWideNarrowIter_Execution ON POLOXI.WideNarrowingIteration(WideExecutionId,RoundNumber) WHERE IsDeleted=0;
END;

-- ── V3.0 configuration settings (DB is the source of truth) ──
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
		(N'Intelligence.SearchWide.EnableAdaptiveNarrowing',N'true',N'Boolean',N'Enables V3.0 evidence-guided adaptive narrowing (branch resolution/reopen, candidate state transitions, discovery admission gate). Fail-soft: failures degrade to V2.x behavior.'),
		(N'Intelligence.SearchWide.NarrowingBranchCoverageFloor',N'0.60',N'Decimal',N'Minimum evidence support a branch needs before it is eligible to be RESOLVED (settled, removed from investigation attention).'),
		(N'Intelligence.SearchWide.NarrowingInformationValueFloor',N'0.35',N'Decimal',N'Adjusted information value below which a well-covered branch no longer justifies investigation and may be RESOLVED.'),
		(N'Intelligence.SearchWide.NarrowingReopenSupportDelta',N'0.15',N'Decimal',N'Absolute change in evidence support that reopens a RESOLVED branch (reversible uncertainty).'),
		(N'Intelligence.SearchWide.NarrowingCandidateCoverageFloor',N'0.50',N'Decimal',N'Minimum relative signal coverage a candidate needs before it is eligible for DEFERRED; below this the candidate is WATCH (missing evidence means investigate, never eliminate).'),
		(N'Intelligence.SearchWide.NarrowingCandidateScoreGap',N'0.40',N'Decimal',N'Minimum relative score gap behind the leader for a well-covered candidate to be DEFERRED.'),
		(N'Intelligence.SearchWide.NarrowingDiscoveryMinimumSupport',N'2',N'Integer',N'Minimum distinct evidence attestations (hosts/mentions) a newly discovered candidate needs to be ADMITTED to the candidate universe.'),
		(N'Intelligence.SearchWide.MaximumCandidateAdmissionsPerRound',N'5',N'Integer',N'Per-round budget for evidence-justified candidate admissions (expansion has a cost; prevents unbounded growth).');

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
