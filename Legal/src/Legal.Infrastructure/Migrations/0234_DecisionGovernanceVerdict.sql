SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Epistemic Authority Layer — EA-7 governance verdict persistence (§34, §35).
--
-- A NON-DESTRUCTIVE override overlay. This table never mutates or replaces the authoritative V2
-- readiness verdict, the projected claims, support edges, or verification events — those all remain
-- fully intact and visible for reference. Instead, per decision session, it records:
--   • a snapshot of the V2 readiness verdict (the reference),
--   • the EA governance verdict (readiness + output audit),
--   • which verdict is EFFECTIVE and under which override mode,
--   • the blockers/violations and the full involved-claim set (JSON) for UI annotation.
--
-- Default mode is Advisory: the effective verdict equals the V2 verdict; EA only annotates. Standard
-- base/audit fields; soft-delete only (records are never physically removed).
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionGovernanceVerdict',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionGovernanceVerdict
(
	GovernanceVerdictId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionGovernanceVerdict PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	MatterId             UNIQUEIDENTIFIER NULL,
	OverrideModeCode     NVARCHAR(20) NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Mode DEFAULT N'Advisory',

	-- Reference: the authoritative V2 verdict snapshot (never altered by this overlay).
	V2ReadinessSatisfied BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_V2Ready DEFAULT 0,

	-- EA governance outcome.
	EaReadinessReady     BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_EaReady DEFAULT 1,
	EaReadinessEnforced  BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_EaEnforced DEFAULT 0,
	OutputClean          BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_OutputClean DEFAULT 1,

	-- The verdict actually surfaced to the user after applying the override mode.
	EffectiveReadinessSatisfied BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_EffReady DEFAULT 1,
	-- True only when EA changed the effective verdict away from the V2 verdict (never in Advisory).
	OverrideApplied      BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Applied DEFAULT 0,

	ProjectedClaimCount  INT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Projected DEFAULT 0,
	AuthorizedClaimCount INT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Authorized DEFAULT 0,
	BlockerCount         INT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Blockers DEFAULT 0,
	ViolationCount       INT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Violations DEFAULT 0,

	-- Reference payloads for UI annotation. ALL involved claims (authorized + unauthorized) are kept.
	BlockersJson         NVARCHAR(MAX) NULL,
	ViolationsJson       NVARCHAR(MAX) NULL,
	InvolvedClaimsJson   NVARCHAR(MAX) NULL,
	NarrativeJson        NVARCHAR(MAX) NULL,

	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionGovernanceVerdict_IsDeleted DEFAULT 0
);

-- One active governance verdict per session (upserted). Non-unique index keeps history soft-deleted rows.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionGovernanceVerdict') AND name=N'IX_Legal_DecisionGovernanceVerdict_SessionId')
	CREATE INDEX IX_Legal_DecisionGovernanceVerdict_SessionId ON POLOXI.Legal_DecisionGovernanceVerdict (DecisionSessionId) WHERE IsDeleted = 0;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_DecisionGovernanceVerdict') AND name=N'IX_Legal_DecisionGovernanceVerdict_MatterId')
	CREATE INDEX IX_Legal_DecisionGovernanceVerdict_MatterId ON POLOXI.Legal_DecisionGovernanceVerdict (MatterId) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
