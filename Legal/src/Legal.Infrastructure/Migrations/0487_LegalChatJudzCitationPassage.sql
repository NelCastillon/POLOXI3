SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Chat Judz — dedicated Citation Passages history for /legal/chatjudz
-- When Judz generates a passage-linked citation result, we ALSO persist the ranked authority landscape
-- as its own first-class, reopenable history (not only inside the chat transcript). A single generation
-- is one "batch" (CitationBatchId); each ranked authority within a branch is one row carrying its own
-- metadata columns (branch, confidence, rank, match score, facets JSON, fit detail, linked-factor count).
-- Self-contained and tenant/user scoped; MatterId and ConversationId are optional tags only (no cross-
-- module FK here — the authoritative Matter FK is enforced by the convergence migration). Standard
-- base/audit fields included. Idempotent and re-runnable.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

IF OBJECT_ID(N'POLOXI.Legal_ChatJudzCitationPassage',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ChatJudzCitationPassage
(
	ChatJudzCitationPassageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ChatJudzCitationPassage PRIMARY KEY DEFAULT NEWID(),
	CitationBatchId           UNIQUEIDENTIFIER NOT NULL,   -- one generation = one reopenable batch
	ConversationId            UNIQUEIDENTIFIER NULL,        -- optional tag: the chat thread it came from
	BatchTitle                NVARCHAR(200) NULL,           -- short label for the batch (e.g. the user ask)
	-- Branch (interpretation) the authority answers.
	BranchDisplayName         NVARCHAR(400) NULL,
	BranchConfidence          DECIMAL(9,6) NULL,
	BranchInterpretation      NVARCHAR(MAX) NULL,
	-- Ranked authority (one row each).
	RankNumber                INT NOT NULL CONSTRAINT DF_Legal_ChatJudzCitationPassage_Rank DEFAULT 0,
	AuthorityName             NVARCHAR(600) NOT NULL,
	MatchScore                DECIMAL(9,6) NULL,
	-- Facets rendered as Label:Value pairs (Doctrine, Jurisdiction, Fact fit, …), stored as JSON.
	FacetsJson                NVARCHAR(MAX) NULL,
	FitDetail                 NVARCHAR(MAX) NULL,           -- the authority passage / fit rationale
	LinkedMatterPassage       NVARCHAR(MAX) NULL,           -- the matched case passage (right column)
	LinkedFactorCount         INT NOT NULL CONSTRAINT DF_Legal_ChatJudzCitationPassage_LinkedCount DEFAULT 0,
	MatterId                  UNIQUEIDENTIFIER NULL,
	TenantId                  UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_ChatJudzCitationPassage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_ChatJudzCitationPassage_IsDeleted DEFAULT 0,
	RowVersion                ROWVERSION
);

GO

-- Batch access path: all rows in one batch for a user, ordered as rendered.
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzCitationPassage',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChatJudzCitationPassage_Batch' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChatJudzCitationPassage'))
	CREATE INDEX IX_Legal_ChatJudzCitationPassage_Batch
		ON POLOXI.Legal_ChatJudzCitationPassage (TenantId, CreatedByUserId, CitationBatchId, RankNumber)
		WHERE IsDeleted = 0;

GO

-- List access path: a user's citation batches, newest first, optional matter filter.
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzCitationPassage',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChatJudzCitationPassage_User' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChatJudzCitationPassage'))
	CREATE INDEX IX_Legal_ChatJudzCitationPassage_User
		ON POLOXI.Legal_ChatJudzCitationPassage (TenantId, CreatedByUserId, MatterId, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

COMMIT TRANSACTION;
