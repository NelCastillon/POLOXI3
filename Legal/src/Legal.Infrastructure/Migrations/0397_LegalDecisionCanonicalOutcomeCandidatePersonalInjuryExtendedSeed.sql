-- ============================================================================
-- 0397: Canonical Decision Outcome Candidates — PERSONAL_INJURY (C6–C9).
--
-- WHY THIS EXISTS:
--   Migration 0396 seeded C1–C5, a generic resolution taxonomy that guarantees a
--   non-starved candidate pool for TYPICAL PI matters. It is intentionally NOT an
--   exhaustive enumeration of every materially-distinct terminal disposition. Real
--   PI matters also resolve through ADR awards, procedural (non-merits) bars,
--   voluntary withdrawal by the plaintiff, and default judgments — each carrying
--   distinct strategy/risk semantics that C1–C5 fold away.
--
-- CORRECTION:
--   Extend the SAME DB-backed source-of-truth pool with four additional canonical
--   outcome candidates so competition can distinguish these resolutions when the
--   matter record supports them. C1–C5 are left UNTOUCHED.
--
--     C6  Arbitration or ADR award            — binding ADR result; neither a court
--                                                verdict (C3/C4) nor a negotiated
--                                                settlement (C1).
--     C7  Procedural or jurisdictional bar     — involuntary dismissal on a
--                                                NON-merits ground (statute of
--                                                limitations, standing, jurisdiction);
--                                                distinct from merits-based C4.
--     C8  Voluntary dismissal or withdrawal     — plaintiff-initiated nonsuit /
--                                                abandonment; distinct from a
--                                                defense-won dismissal (C4).
--     C9  Default judgment                      — judgment entered on a defendant's
--                                                failure to appear or defend.
--
--   All four are prospective RESOLUTION PATHWAYS (RoleCode = PATHWAY). The shared
--   determining FACTORS (liability, statutory applicability, comparative fault,
--   causation, damages) remain the L1->Ln evaluation hierarchy UNDER each candidate
--   and are NOT seeded here.
--
-- Depends on the table created by migration 0396. Global defaults (TenantId NULL),
-- standard base/audit fields, idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ── Seed C6–C9 under the PERSONAL_INJURY domain pack (global default) ────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
	AND OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
BEGIN
	DECLARE @Outcomes TABLE (OutcomeCode NVARCHAR(60), Name NVARCHAR(200), Description NVARCHAR(1000), RoleCode NVARCHAR(40), RequiresVerification BIT, SortOrder INT);
	INSERT INTO @Outcomes (OutcomeCode, Name, Description, RoleCode, RequiresVerification, SortOrder) VALUES
		(N'C6', N'Arbitration or ADR award',
		 N'A binding result reached through arbitration or another alternative dispute resolution forum. Distinct from a court verdict and from a privately negotiated settlement; the statutory theory and comparative fault are resolved by the arbitrator rather than a judge or jury.',
		 N'PATHWAY', 0, 60),
		(N'C7', N'Procedural or jurisdictional bar',
		 N'An involuntary dismissal on a NON-merits ground such as statute of limitations, lack of standing, or want of jurisdiction. The claim is defeated without the statutory violation, causation, or comparative fault being adjudicated, so it is distinct from a merits-based defense judgment.',
		 N'PATHWAY', 0, 70),
		(N'C8', N'Voluntary dismissal or withdrawal',
		 N'A plaintiff-initiated nonsuit, voluntary dismissal, or abandonment of the claim. Distinct from a defense-won dismissal: the defense does not prevail on the merits, and the plaintiff chooses to discontinue the action.',
		 N'PATHWAY', 0, 80),
		(N'C9', N'Default judgment',
		 N'A judgment entered because the defendant failed to appear or defend. Liability follows from the default rather than a contested adjudication of the statutory theory, causation, or comparative fault.',
		 N'PATHWAY', 0, 90);

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
