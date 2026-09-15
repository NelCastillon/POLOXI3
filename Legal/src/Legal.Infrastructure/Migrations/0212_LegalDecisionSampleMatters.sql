SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Sample Matters (/legal/matters demo data)
-- Seeds 2 sample attorney-facing matters and a complete, self-consistent set of related decision
-- records so the matter dashboard, matter detail, decision cockpit, and timeline render real
-- DB-backed state on a fresh environment. Idempotent: keyed on deterministic GUIDs so re-running
-- (or the startup migrator re-applying) never duplicates rows. All rows are tenant-scoped to the
-- development demo tenant (00000000-0000-0000-0000-000000000001) used by the dev auth handler.
--
-- Seeded per matter:
--   Legal_DecisionMatter          → the dashboard card
--   Legal_DecisionSession         → the persisted decision run (linked via MatterId + NBA fields)
--   Legal_DecisionCandidate       → competing outcomes (winner + runner-up)
--   Legal_DecisionBranch          → interpretation branches (incl. frontier)
--   Legal_DecisionEvidence        → supporting authorities
--   Legal_DecisionFlipPoint       → what could overturn the winner (critical flip points)
--   Legal_DecisionDependency      → decision dependency nodes
--   Legal_DecisionEvent           → event-sourced timeline
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant     UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User       UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now        DATETIME2        = SYSUTCDATETIME();

-- Deterministic IDs ─ Matter 1 (Acme v. Globex — Summary Judgment)
DECLARE @M1   UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000001';
DECLARE @S1   UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000010';
DECLARE @S1C1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000021';
DECLARE @S1C2 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000022';
DECLARE @S1B1 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000031';
DECLARE @S1B2 UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000032';

-- Deterministic IDs ─ Matter 2 (Meridian Insurance Coverage Dispute)
DECLARE @M2   UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000001';
DECLARE @S2   UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000010';
DECLARE @S2C1 UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000021';
DECLARE @S2C2 UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000022';
DECLARE @S2B1 UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000031';
DECLARE @S2B2 UNIQUEIDENTIFIER = N'A2000000-0000-0000-0000-000000000032';

-- ── Matters ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M1)
	INSERT POLOXI.Legal_DecisionMatter (DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@M1, N'Acme v. Globex - Summary Judgment', N'Summary Judgment', N'N.D. Cal.', N'Dispositive motion',
		N'Defendant''s motion for summary judgment on the breach of contract and misappropriation claims. Central question is whether the NDA''s non-use provision survived termination.',
		N'OPEN', @Tenant, DATEADD(DAY, -12, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M2)
	INSERT POLOXI.Legal_DecisionMatter (DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@M2, N'Meridian Insurance - Coverage Dispute', N'Coverage Dispute', N'S.D.N.Y.', N'Pre-litigation',
		N'Whether the commercial general liability policy''s pollution exclusion bars coverage for the underlying environmental cleanup claim.',
		N'OPEN', @Tenant, DATEADD(DAY, -6, @Now), @User);

-- ── Sessions (linked to matter + persisted Next Best Action) ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S1)
	INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
		TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
		DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
		NextBestActionRationale, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S1,
		N'Should the court grant summary judgment for the defendant on the breach of the NDA''s non-use provision?',
		N'LEGAL', N'gpt-4.1-mini', 1, N'COMPLETED', N'RESOLVED', N'Decision margin exceeded threshold', @S1C1,
		0.8600, 0.412000, 0.230000, 3, 7, 8450,
		N'On the current record, summary judgment is likely to be DENIED. A triable issue of material fact exists over whether Globex used the confidential specifications after the NDA terminated, which the non-use clause expressly survives.',
		@M1, N'Depose the Globex engineer identified in the source-control logs to resolve the disputed use-after-termination fact.',
		N'HIGH',
		N'This is the single fact on the decision frontier with the highest flip potential; resolving it either confirms denial or opens a path to judgment.',
		@Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @S2)
	INSERT POLOXI.Legal_DecisionSession (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode,
		TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin,
		DepthReached, LlmCallCount, DurationMs, FinalAnswer, MatterId, NextBestActionText, NextBestActionImpactCode,
		NextBestActionRationale, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S2,
		N'Does the CGL policy''s absolute pollution exclusion bar coverage for the environmental cleanup claim?',
		N'LEGAL', N'gpt-4.1-mini', 1, N'COMPLETED', N'RESOLVED', N'Decision margin exceeded threshold', @S2C1,
		0.7900, 0.523000, 0.140000, 2, 6, 7220,
		N'Coverage is more likely than not BARRED. The absolute pollution exclusion, as construed under New York law, applies to the gradual contamination alleged, and no exception is triggered on the pleaded facts.',
		@M2, N'Obtain the policy''s complete forms schedule to confirm no buy-back endorsement modifies the absolute pollution exclusion.',
		N'MEDIUM',
		N'A buy-back endorsement would materially change the exclusion analysis; confirming its absence hardens the current conclusion.',
		@Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Candidates (winner + runner-up per session) ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S1C1)
	INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
		LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
		RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S1C1, @S1, N'C1', N'Deny summary judgment', N'A genuine dispute of material fact over post-termination use precludes judgment.',
		0.8200, 0.7400, 0.7000, 0.8000, 0.7600, 0.2400, 0.7200, 0.6800, 0.762000, 1, 1, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S1C2)
	INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
		LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
		RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S1C2, @S1, N'C2', N'Grant summary judgment', N'No admissible evidence of use after termination; non-use clause not breached.',
		0.5800, 0.4200, 0.4600, 0.6100, 0.5200, 0.4800, 0.5400, 0.5000, 0.532000, 2, 0, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S2C1)
	INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
		LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
		RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S2C1, @S2, N'C1', N'Coverage barred', N'The absolute pollution exclusion applies to the gradual contamination alleged.',
		0.7800, 0.7000, 0.6600, 0.7500, 0.7100, 0.2900, 0.6800, 0.6400, 0.716000, 1, 1, @Tenant, DATEADD(DAY, -5, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionCandidate WHERE DecisionCandidateId = @S2C2)
	INSERT POLOXI.Legal_DecisionCandidate (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome,
		LegalSupport, FactSupport, EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination,
		RankingImpact, CompositeScore, RankOrder, IsWinner, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S2C2, @S2, N'C2', N'Coverage exists', N'Sudden-and-accidental exception or ambiguity resolves in favor of the insured.',
		0.6400, 0.5800, 0.5200, 0.6000, 0.5600, 0.4400, 0.5800, 0.5400, 0.576000, 2, 0, @Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Branches (interpretation branches, incl. frontier) ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S1B1)
	INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
		Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
		AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S1B1, @S1, 1, N'B1', N'Did use occur after termination?',
		N'The non-use clause survives termination; the dispositive fact is whether Globex used the specs afterward.',
		N'ACTIVE', 0.720000, 0.8100, 0.6600, 0.5500, 0.680000, 1, 1, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S1B2)
	INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
		Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
		AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S1B2, @S1, 1, N'B2', N'Was the confidentiality clause enforceable?',
		N'Whether the NDA''s scope is reasonable and enforceable under California law.',
		N'RESOLVED', 0.410000, 0.5200, 0.2800, 0.7800, 0.360000, 0, 2, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S2B1)
	INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
		Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
		AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S2B1, @S2, 1, N'B1', N'Is the contamination "gradual" or "sudden and accidental"?',
		N'The exclusion''s reach turns on the nature of the discharge alleged in the underlying complaint.',
		N'ACTIVE', 0.640000, 0.7600, 0.5800, 0.6000, 0.610000, 1, 1, @Tenant, DATEADD(DAY, -5, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionBranch WHERE DecisionBranchId = @S2B2)
	INSERT POLOXI.Legal_DecisionBranch (DecisionBranchId, DecisionSessionId, LevelNumber, BranchCode, DisplayName,
		Interpretation, BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability,
		AdvScore, IsOnFrontier, SortOrder, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (@S2B2, @S2, 1, N'B2', N'Does a buy-back endorsement modify the exclusion?',
		N'A pollution buy-back endorsement would restore coverage notwithstanding the absolute exclusion.',
		N'ACTIVE', 0.480000, 0.6200, 0.5200, 0.4000, 0.440000, 0, 2, @Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Evidence ───────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A1000000-0000-0000-0000-000000000041')
	INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
		Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A1000000-0000-0000-0000-000000000041', @S1, @S1B1, N'Celotex Corp. v. Catrett, 477 U.S. 317 (1986)',
		N'Celotex Corp. v. Catrett', N'Summary judgment is proper only where there is no genuine dispute of material fact.',
		0.9000, 0.8200, 0.780000, N'VERIFIED', @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvidence WHERE DecisionEvidenceId = N'A2000000-0000-0000-0000-000000000041')
	INSERT POLOXI.Legal_DecisionEvidence (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle,
		Snippet, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A2000000-0000-0000-0000-000000000041', @S2, @S2B1, N'Belt Painting Corp. v. TIG Ins. Co., 100 N.Y.2d 377 (2003)',
		N'Belt Painting Corp. v. TIG Ins. Co.', N'Pollution exclusions are construed in light of the reasonable expectations of the insured.',
		0.8500, 0.7600, 0.710000, N'VERIFIED', @Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Flip points (critical → drives the "at risk" KPI on the dashboard) ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A1000000-0000-0000-0000-000000000051')
	INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
		ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A1000000-0000-0000-0000-000000000051', @S1, @S1B1,
		N'If discovery shows no post-termination use, the winner flips from "Deny" to "Grant" summary judgment.',
		0.230000, 1, 1, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionFlipPoint WHERE DecisionFlipPointId = N'A2000000-0000-0000-0000-000000000051')
	INSERT POLOXI.Legal_DecisionFlipPoint (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description,
		ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A2000000-0000-0000-0000-000000000051', @S2, @S2B2,
		N'A pollution buy-back endorsement in the forms schedule would flip the outcome to "Coverage exists".',
		0.140000, 1, 1, @Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Dependencies ───────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A1000000-0000-0000-0000-000000000061')
	INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
		Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A1000000-0000-0000-0000-000000000061', @S1, @S1C1, N'ELEMENT',
		N'The non-use obligation survived termination of the NDA.', 0.8400, 1, @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDependency WHERE DecisionDependencyId = N'A2000000-0000-0000-0000-000000000061')
	INSERT POLOXI.Legal_DecisionDependency (DecisionDependencyId, DecisionSessionId, DecisionCandidateId, NodeKind, Statement,
		Support, IsEssential, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES (N'A2000000-0000-0000-0000-000000000061', @S2, @S2C1, N'ELEMENT',
		N'The discharge alleged is "gradual" contamination within the exclusion.', 0.7300, 1, @Tenant, DATEADD(DAY, -5, @Now), @User);

-- ── Events (timeline) ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S1)
	INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(N'A1000000-0000-0000-0000-000000000071', @S1, 1, N'SESSION_STARTED',   N'DISCOVERY', N'{"query":"NDA non-use summary judgment"}', @Tenant, DATEADD(DAY, -11, @Now), @User),
		(N'A1000000-0000-0000-0000-000000000072', @S1, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY', N'{"count":2}', @Tenant, DATEADD(DAY, -11, @Now), @User),
		(N'A1000000-0000-0000-0000-000000000073', @S1, 3, N'FRONTIER_UPDATED',  N'RANKING',   N'{"frontier":["B1"]}', @Tenant, DATEADD(DAY, -11, @Now), @User),
		(N'A1000000-0000-0000-0000-000000000074', @S1, 4, N'DECISION_RESOLVED', N'ANSWER',    N'{"winner":"C1","margin":0.23}', @Tenant, DATEADD(DAY, -11, @Now), @User);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @S2)
	INSERT POLOXI.Legal_DecisionEvent (DecisionEventId, DecisionSessionId, SequenceNumber, EventType, StageCode, PayloadJson, TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(N'A2000000-0000-0000-0000-000000000071', @S2, 1, N'SESSION_STARTED',   N'DISCOVERY', N'{"query":"pollution exclusion coverage"}', @Tenant, DATEADD(DAY, -5, @Now), @User),
		(N'A2000000-0000-0000-0000-000000000072', @S2, 2, N'CANDIDATES_PROPOSED', N'DISCOVERY', N'{"count":2}', @Tenant, DATEADD(DAY, -5, @Now), @User),
		(N'A2000000-0000-0000-0000-000000000073', @S2, 3, N'FRONTIER_UPDATED',  N'RANKING',   N'{"frontier":["B1"]}', @Tenant, DATEADD(DAY, -5, @Now), @User),
		(N'A2000000-0000-0000-0000-000000000074', @S2, 4, N'DECISION_RESOLVED', N'ANSWER',    N'{"winner":"C1","margin":0.14}', @Tenant, DATEADD(DAY, -5, @Now), @User);

COMMIT TRANSACTION;
