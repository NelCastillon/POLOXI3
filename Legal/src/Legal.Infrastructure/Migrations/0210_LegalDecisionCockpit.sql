SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Cockpit (/legal/decision)
-- Adds the Matter aggregate that groups decision sessions into an attorney-facing "matter" and
-- links each decision session to its matter. Also persists the deterministically-derived
-- Next Best Action on the session so the cockpit and the matter dashboard render real DB-backed
-- state rather than recomputing on read. Follows Table → API → UI order; all objects live in the
-- POLOXI schema, are prefixed Legal_Decision*, and carry the standard base/audit fields.
-- The /legal/search Intelligence Wide tables and the 0209 decision tables are left intact.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Matter: attorney-facing aggregate that groups decision sessions (dashboard card source). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionMatter
(
	DecisionMatterId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionMatter PRIMARY KEY DEFAULT NEWID(),
	Title                NVARCHAR(300) NOT NULL,
	MatterTypeCode       NVARCHAR(80) NULL,        -- e.g. Summary Judgment, Coverage, Contract (free/config text)
	Jurisdiction         NVARCHAR(120) NULL,
	Posture              NVARCHAR(200) NULL,
	Description          NVARCHAR(MAX) NULL,
	StatusCode           NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionMatter_Status DEFAULT N'OPEN',
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionMatter_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionMatter_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Legal_DecisionMatter_Tenant',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionMatter_Tenant ON POLOXI.Legal_DecisionMatter (TenantId, ModifiedDateUtc DESC, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

-- ── Link decision sessions to a matter + persist the derived Next Best Action (§ next action). ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'MatterId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD MatterId UNIQUEIDENTIFIER NULL;

GO

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'NextBestActionText') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD NextBestActionText NVARCHAR(MAX) NULL;

GO

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'NextBestActionImpactCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD NextBestActionImpactCode NVARCHAR(40) NULL;

GO

IF COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'NextBestActionRationale') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionSession ADD NextBestActionRationale NVARCHAR(MAX) NULL;

GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_DecisionSession_Matter')
   AND COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'MatterId') IS NOT NULL
	ALTER TABLE POLOXI.Legal_DecisionSession
		ADD CONSTRAINT FK_Legal_DecisionSession_Matter FOREIGN KEY (MatterId)
			REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId);

GO

IF OBJECT_ID(N'IX_Legal_DecisionSession_Matter',N'IX') IS NULL
   AND COL_LENGTH(N'POLOXI.Legal_DecisionSession', N'MatterId') IS NOT NULL
	CREATE INDEX IX_Legal_DecisionSession_Matter ON POLOXI.Legal_DecisionSession (MatterId, CreatedDateUtc DESC) WHERE IsDeleted = 0;

GO

COMMIT TRANSACTION;
