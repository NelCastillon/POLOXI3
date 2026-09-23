SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — GENERAL TEST MATTER (M10 / S10).
--
-- A complete, self-contained test matter for /legal/matters and the decision cockpit: the matter
-- (with structured metadata columns the list/detail query reads), a linked decision session (with a
-- persisted Next Best Action), two ranked candidates (winner + runner-up), two decision branches
-- (one resolved, one on the frontier), and verified evidence.
--
-- IMPORTANT: uses the BA000000-* GUID range. The earlier 0237 attempt used the A7000000-* range,
-- which had ALREADY been consumed by an existing seed matter (Aspen Textiles v. Birchwood Supply),
-- so its IF NOT EXISTS guard matched that row and silently skipped the insert. This file uses a
-- previously unused GUID prefix so the row is actually created, and ships as a new filename because
-- LegalDatabaseMigrator tracks applied scripts by filename and never re-runs an applied name.
--
-- Idempotent + tenant-scoped to the development demo tenant (00000000-…-0001).
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();

-- Deterministic ids ── Matter 10 (Acme v. Contoso — summary judgment). Unused BA000000-* range.
DECLARE @M    UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000001';
DECLARE @S    UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000010';
DECLARE @C1   UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000021';  -- winner (SJ granted on breach)
DECLARE @C2   UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000022';  -- runner-up (SJ denied on bad-faith)
DECLARE @B1   UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000031';  -- resolved dispositive branch
DECLARE @B2   UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000032';  -- open frontier branch
DECLARE @E1   UNIQUEIDENTIFIER = N'BA000000-0000-0000-0000-000000000041';  -- verified evidence

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND OBJECT_ID(N'POLOXI.Legal_DecisionSession', N'U') IS NOT NULL
BEGIN
	-- ── Matter (with structured metadata columns read by the list/detail query). ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M AND TenantId = @Tenant)
		INSERT POLOXI.Legal_DecisionMatter
			(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
			 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
			 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
			 TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@M,
			 N'Test Matter — Acme v. Contoso (Summary Judgment)',
			 N'Summary Judgment',
			 N'United States - California - District Court - Northern District - Governing law: California',
			 N'Motion for summary judgment - Movant: Contoso - Target: all claims - Requested disposition: judgment as a matter of law',
			 N'General end-to-end test matter used to validate the Legal decision cockpit, matter dashboard, candidate ranking, and branch panels. Summary judgment is likely on the breach claim but a genuine dispute of material fact remains on the bad-faith claim.',
			 N'OPEN',
			 N'Contract',
			 N'United States - Federal',
			 N'California',
			 N'District Court',
			 N'Northern District of California',
			 N'California',
			 N'Contoso',
			 N'Acme',
			 N'All claims (breach and bad-faith)',
			 N'Judgment as a matter of law',
			 @Tenant, DATEADD(DAY, -2, @Now), @User);

	-- ── Session linked to the matter, with a persisted Next Best Action. ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S)
		INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
			TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
			DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
			NextBestActionRationale, UseDependencyGraph, ReadinessSatisfied, ReadinessBlockersJson, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@S,
			N'Will Contoso''s motion for summary judgment on all claims be granted in the Northern District of California?',
			N'LEGAL', N'gpt-4.1-mini', 1, N'PROVISIONAL_DECISION', N'PROVISIONAL_DECISION',
			N'Leading outcome separates on the breach claim, but an open high-value branch remains on the bad-faith claim',
			@C1, 0.9200, 0.550000, 0.280000,
			3, 7, 4200,
			N'Summary judgment is likely to be GRANTED on the breach claim (no genuine dispute of material fact) but DENIED on the bad-faith claim, where a genuine dispute of material fact remains on the record.',
			@M,
			N'Obtain the underwriter''s deposition transcript to close the evidentiary gap on the bad-faith claim.',
			N'HIGH',
			N'The bad-faith candidate has the lowest evidence support and the highest flip potential; targeted discovery on the underwriter''s conduct maximizes information value and could change the disposition on that claim.',
			1, 0, N'["Bad-faith claim: material fact dispute unresolved on the current record"]',
			@Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Candidates (winner + runner-up). ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @C1)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@C1, @S, N'C1', N'SJ granted on breach claim',
			N'Summary judgment granted: no genuine dispute of material fact on the breach claim.',
			0.8800, 0.8500, 0.8200, 0.9000, 0.8600, 0.1400, 0.7200, 0.6800, 0.842000, 1, 1, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @C2)
		INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
			LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
			RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@C2, @S, N'C2', N'SJ denied on bad-faith claim',
			N'Summary judgment denied: genuine dispute of material fact on the bad-faith claim.',
			0.6200, 0.5800, 0.4100, 0.7000, 0.5200, 0.4200, 0.6100, 0.5900, 0.561000, 2, 0, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Branches (one resolved, one still on the frontier). ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @B1)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@B1, @S, 1, N'C1.B1', N'Genuine dispute of material fact on the breach claim?',
			N'Whether the record shows a triable issue of fact on the breach claim. The material terms and performance are undisputed — resolved for the movant.',
			N'RESOLVED', 0.720000, 0.9000, 0.3000, 0.8500, 0.640000, 0, 10, @Tenant, DATEADD(DAY, -1, @Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @B2)
		INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
			Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
			AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@B2, @S, 1, N'C2.B1', N'Sufficiency of bad-faith evidence',
			N'Whether the evidence of the insurer''s conduct meets the bad-faith threshold. A material fact dispute remains — high-value discovery is still open.',
			N'ACTIVE', 0.810000, 0.8000, 0.6500, 0.4000, 0.700000, 1, 20, @Tenant, DATEADD(DAY, -1, @Now), @User);

	-- ── Evidence (verified, supporting the resolved dispositive branch). ──
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = @E1)
		INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
			Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@E1, @S, @B1, N'Executed master services agreement §4 (payment terms) and undisputed non-payment record',
			N'MSA §4 — payment terms',
			N'The written contract''s payment obligation and the fact of non-payment are undisputed on the record, leaving no triable issue on the breach element.',
			0.9000, 0.8800, 0.900000, N'VERIFIED', @Tenant, DATEADD(DAY, -1, @Now), @User);
END;

COMMIT TRANSACTION;
