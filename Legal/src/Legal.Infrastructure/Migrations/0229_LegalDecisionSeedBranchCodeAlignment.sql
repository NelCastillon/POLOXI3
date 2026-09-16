SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — SEED BRANCH-CODE ALIGNMENT TO LIVE CONVENTION.
--
-- Live decision runs (ScoreCandidates) emit candidate codes C1/C2 and branch codes "C{n}.B{m}"
-- ("C1.B1", "C2.B1", ...). The regression-seed matters (0223/0224/0225/0227) originally used bare
-- branch codes ("B1", "B2"). Both the flip-point identity logic (BuildFlipPoints) and the cockpit's
-- candidate-attribution label (CandidateLabelForBranch) map a branch to its owning candidate via
-- BranchCode.Split('.')[0]. Bare "B1"/"B2" match no candidate, so seeded sessions silently lost
-- candidate attribution and could not participate correctly in identity-based flip evaluation.
--
-- The seed INSERTs are guarded by IF NOT EXISTS on deterministic GUIDs, so editing them only helps
-- FRESH databases. This forward migration realigns branch codes on databases where those seeds have
-- ALREADY run. Mapping mirrors live semantics: the flip-driving branch belongs to the CHALLENGER
-- (non-winner) candidate, and the winner-supporting branch belongs to the WINNER candidate.
--
--   0223 (S4): B1 → C2.B1 (flip→Deny),      B2 → C1.B1 (winner support)
--   0224 (S5): B1 → C2.B1 (flip→Coverage),  B2 → C1.B1 (winner support)
--   0225 (S6): B1 → C1.B1 (winner support),  B2 → C2.B1 (challenger tolling theory)
--   0227 (S7): B1 → C2.B1 (flip→Purchase Order), B2 → C1.B1 (winner support)
--
-- Idempotent: keyed on the deterministic branch GUIDs; safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionBranch', N'U') IS NOT NULL
BEGIN
	-- 0223 — Halcyon Labs (preliminary injunction). Winner C1 = Grant.
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C2.B1' WHERE DecisionBranchId = N'A4000000-0000-0000-0000-000000000031';
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C1.B1' WHERE DecisionBranchId = N'A4000000-0000-0000-0000-000000000032';

	-- 0224 — Meridian Mutual (coverage / exclusion). Winner C1 = No coverage.
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C2.B1' WHERE DecisionBranchId = N'A5000000-0000-0000-0000-000000000031';
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C1.B1' WHERE DecisionBranchId = N'A5000000-0000-0000-0000-000000000032';

	-- 0225 — Riverside Logistics (statute of limitations). Winner C1 = Time-barred.
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C1.B1' WHERE DecisionBranchId = N'A6000000-0000-0000-0000-000000000031';
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C2.B1' WHERE DecisionBranchId = N'A6000000-0000-0000-0000-000000000032';

	-- 0227 — Aspen Textiles (which contract governs). Winner C1 = Master Supply Agreement.
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C2.B1' WHERE DecisionBranchId = N'A7000000-0000-0000-0000-000000000031';
	UPDATE POLOXI.Legal_DecisionBranch SET BranchCode = N'C1.B1' WHERE DecisionBranchId = N'A7000000-0000-0000-0000-000000000032';
END

COMMIT TRANSACTION;
