-- ============================================================================
-- 0423: Personal Injury Domain Pack — decision-dimension gap seed.
--
-- PURPOSE:
--   The PERSONAL_INJURY domain pack already carries a rich dimension catalog
--   seeded across migrations 0284 (baseline) and 0405 (enterprise expansion).
--   A taxonomy review proposed 20 L1 dimensions; most were already present by
--   code OR by concept (e.g. proposed MITIGATION == existing DAMAGES_MITIGATION,
--   PARTIES_CAPACITY == existing PARTIES, EVIDENCE_PROVENANCE == existing
--   EVIDENCE). This migration seeds ONLY the dimensions that have no existing
--   equivalent and were explicitly approved:
--
--     * CLAIM_FRAMEWORK  — Applicable Claim Framework (claim theory/standard)
--     * INCIDENT_FACTS   — Incident Reconstruction (event sequence/conditions)
--     * TREATMENT        — Treatment Course (chronology/gaps/MMI)
--     * EXPERT_SUPPORT   — Expert Evidence (methodology/admissibility)
--     * RESOLUTION       — Resolution & Practical Constraints (approved despite
--                          overlap with SETTLEMENT/SETTLEMENT_VALUATION)
--
--   Deliberately NOT added:
--     * All codes already present by exact match (DUTY, BREACH, CAUSATION,
--       COMPARATIVE_FAULT, INJURY, DAMAGES_ECONOMIC, DAMAGES_NONECONOMIC,
--       DAMAGES_PUNITIVE, DEFENSES, INSURANCE_COVERAGE, LIENS_SUBROGATION).
--     * Concept duplicates: MITIGATION, PARTIES_CAPACITY, EVIDENCE_PROVENANCE.
--     * PROCEDURE_TIMING — reviewer chose to keep the existing finer-grained
--       PROCEDURE + LIMITATIONS + JURISDICTION_VENUE dimensions instead.
--
-- SCOPE / SAFETY:
--   * Advisory configuration only — POLOXI Core owns all decision reasoning.
--   * Global defaults (TenantId NULL); tenant rows may override later.
--   * Fully idempotent: keyed by DimensionCode and inserted only WHEN NOT
--     MATCHED BY TARGET, so re-running is a no-op. Existing rows are never
--     updated or deleted.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Resolve the global PERSONAL_INJURY pack (TenantId NULL) ──────────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	-- ── Approved gap dimensions ─────────────────────────────────────────────
	-- SortOrder slots each new dimension next to its conceptual neighbor in the
	-- existing 10–92 band without colliding with seeded values:
	--   CLAIM_FRAMEWORK 12 (after LIABILITY 10, before DUTY 15)
	--   INCIDENT_FACTS  18 (liability/breach cluster, after BREACH 16, before INJURY 20)
	--   TREATMENT       22 (medical chain, right after INJURY 20)
	--   EXPERT_SUPPORT  52 (proof axis, right after EVIDENCE 50)
	--   RESOLUTION      92 (resolution tail, after SETTLEMENT/SETTLEMENT_VALUATION 90/91)
	--
	-- PoloxiImportanceScore (0.0000–1.0000) is the advisory interpretation prior
	-- used in Domain Pack (checked) mode only, anchored to the 0407 rubric:
	-- prima-facie elements highest; proof/support axes high; resolution lower.
	-- POLOXI Core still owns all authoritative scoring, competition, and
	-- normalization — this value is never a conclusion or an exclusion.
	MERGE POLOXI.Legal_DecisionDomainPackDimension AS target
	USING (VALUES
		(N'CLAIM_FRAMEWORK', N'Applicable Claim Framework', N'Claim theory, governing jurisdiction and date, applicable standard, statutory theory, and special claim requirements.', 12, CAST(0.8200 AS DECIMAL(5,4))),
		(N'INCIDENT_FACTS',  N'Incident Reconstruction',    N'Event sequence, actor identity, location, timing, movement, environmental conditions, and competing accounts.', 18, CAST(0.8500 AS DECIMAL(5,4))),
		(N'TREATMENT',       N'Treatment Course',           N'Provider attribution, treatment chronology and purpose, gaps, adherence, response, additional-care recommendations, and prognosis/MMI assertions.', 22, CAST(0.7400 AS DECIMAL(5,4))),
		(N'EXPERT_SUPPORT',  N'Expert Evidence',            N'Relevant expertise, materials considered, methodology, assumptions, causal opinions, alternative explanations, opinion limitations, and admissibility review.', 52, CAST(0.7400 AS DECIMAL(5,4))),
		(N'RESOLUTION',      N'Resolution & Practical Constraints', N'Client objective, settlement authority, demands/offers, disputed terms, litigation cost/timing, collectability, approvals, and historical resolution verification.', 92, CAST(0.5600 AS DECIMAL(5,4)))
	) AS source (DimensionCode, Name, Description, SortOrder, PoloxiImportanceScore)
	ON target.DecisionDomainPackId = @PiPackId AND target.DimensionCode = source.DimensionCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, DimensionCode, Name, Description, SortOrder, PoloxiImportanceScore, TenantId)
		VALUES (@PiPackId, source.DimensionCode, source.Name, source.Description, source.SortOrder, source.PoloxiImportanceScore, NULL);
END

COMMIT TRANSACTION;
