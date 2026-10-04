-- ============================================================================
-- 0396: Canonical Decision Outcome Candidates — PERSONAL_INJURY (C1–C5).
--
-- ROOT CAUSE THIS FIXES:
--   On a legal EVALUATE run the Decision Outcome candidate pool is built ENTIRELY
--   from emergent sources (proper-noun harvest over retrieved snippets + one mini
--   LLM enumeration call). Nothing guarantees the five canonical PI resolution
--   outcomes are present, so they surface only as interpretive NARRATIVE text under
--   a single L1 branch and never enter the candidate universe. The Decision Outcome
--   cards (_response.Candidates / FinalRankedCandidates) therefore stay starved,
--   competition delivers < 2 candidates => REGISTERED_SCORING_BLOCKED => Provisional.
--
-- CORRECTION:
--   Make the canonical outcome candidates DB-backed (source of truth) so the
--   application layer can union them into the candidate universe at the start of a
--   legal EVALUATE run. The determining FACTORS (liability, statutory applicability,
--   comparative fault, causation, damages) remain the shared L1->Ln evaluation
--   hierarchy UNDER each candidate; they are NOT outcome candidates and are not
--   seeded here.
--
--   C1–C4 are prospective RESOLUTION PATHWAYS (RoleCode = PATHWAY).
--   C5 is an ASSERTED HISTORICAL STATUS (RoleCode = ASSERTED_HISTORICAL): it must be
--   verified FIRST and never auto-promoted to an established resolution. RequiresVerification = 1.
--
-- New child table of the domain pack (same shape/conventions as the other
-- Legal_DecisionDomainPack* child tables), global defaults (TenantId NULL),
-- standard base/audit fields, idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Canonical outcome-candidate table ───────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate
(
	DecisionDomainPackOutcomeCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackOutcomeCandidate PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackOutcomeCandidate_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	OutcomeCode                          NVARCHAR(60) NOT NULL,          -- C1 | C2 | C3 | C4 | C5
	Name                                 NVARCHAR(200) NOT NULL,         -- the candidate label shown on the Decision Outcome cards
	Description                          NVARCHAR(1000) NULL,
	RoleCode                             NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_Role DEFAULT N'PATHWAY', -- PATHWAY | ASSERTED_HISTORICAL
	RequiresVerification                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_ReqVerify DEFAULT 0,
	MatterTypeCode                       NVARCHAR(120) NULL,
	SortOrder                            INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_SortOrder DEFAULT 0,
	IsActive                             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_IsActive DEFAULT 1,
	TenantId                             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                      DATETIME2 NULL,
	ModifiedByUserId                     UNIQUEIDENTIFIER NULL,
	IsDeleted                            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidate_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate') AND name = N'IX_Legal_DecisionDomainPackOutcomeCandidate_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackOutcomeCandidate_Pack
		ON POLOXI.Legal_DecisionDomainPackOutcomeCandidate (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (OutcomeCode) WHERE IsDeleted = 0;

GO

-- ── Seed C1–C5 under the PERSONAL_INJURY domain pack (global default) ────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	DECLARE @Outcomes TABLE (OutcomeCode NVARCHAR(60), Name NVARCHAR(200), Description NVARCHAR(1000), RoleCode NVARCHAR(40), RequiresVerification BIT, SortOrder INT);
	INSERT INTO @Outcomes (OutcomeCode, Name, Description, RoleCode, RequiresVerification, SortOrder) VALUES
		(N'C1', N'Confidential negotiated settlement',
		 N'A negotiated settlement that avoids the uncertainty of proving the statutory theory and resolving disputed comparative fault at trial.',
		 N'PATHWAY', 0, 10),
		(N'C2', N'Continued negotiation or mediation',
		 N'Further negotiation or mediation without a concluded settlement, allowing the parties to test the statutory basis, the incident account, and comparative-fault positions before committing to an outcome.',
		 N'PATHWAY', 0, 20),
		(N'C3', N'Plaintiff-favorable adjudication or verdict',
		 N'An adjudicated result for the plaintiff, depending on proof that an applicable statute imposed a duty, was violated, and bears the required causal relationship to the claimed harm, with comparative fault addressed.',
		 N'PATHWAY', 0, 30),
		(N'C4', N'Defense-favorable judgment or dismissal',
		 N'A judgment or dismissal for the defense, plausible if the statute does not apply, the alleged violation or causal link is not proven, or a defense defeats the claim.',
		 N'PATHWAY', 0, 40),
		(N'C5', N'Recorded settlement disbursement',
		 N'An ASSERTED HISTORICAL STATUS: a recorded disbursement that may indicate an already-completed resolution. Must be verified FIRST; the entry alone does not establish a statutory violation or resolve comparative fault. Never auto-promoted to an established resolution.',
		 N'ASSERTED_HISTORICAL', 1, 50);

	INSERT INTO POLOXI.Legal_DecisionDomainPackOutcomeCandidate
		(DecisionDomainPackOutcomeCandidateId, DecisionDomainPackId, OutcomeCode, Name, Description, RoleCode, RequiresVerification, MatterTypeCode, SortOrder, IsActive, TenantId)
	SELECT NEWID(), @PiPackId, o.OutcomeCode, o.Name, o.Description, o.RoleCode, o.RequiresVerification, N'PERSONAL_INJURY', o.SortOrder, 1, NULL
	FROM @Outcomes o
	WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidate x
					  WHERE x.DecisionDomainPackId = @PiPackId AND x.OutcomeCode = o.OutcomeCode AND x.TenantId IS NULL AND x.IsDeleted = 0);
END

GO

COMMIT TRANSACTION;
GO
