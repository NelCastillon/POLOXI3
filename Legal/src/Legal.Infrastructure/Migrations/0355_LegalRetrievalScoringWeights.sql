SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Phase E1 — Configurable hybrid retrieval scoring. Promotes the previously hard-coded blend weights
-- (0.65 vector / 0.35 keyword / 0.05 authoritative / 0.05 verified) and candidate limits into DB-backed
-- Legal_DecisionSetting rows so retrieval ranking can be tuned and benchmarked without a code change.
--
-- Seeded values reproduce the exact prior behavior, so production ranking is unchanged until an
-- operator deliberately re-tunes them. FusionStrategy defaults to WeightedScore (the established
-- baseline); ReciprocalRankFusion is scaffolded for future Phase E3 benchmarking and is not wired into
-- the production scoring path yet. Idempotent MERGE — safe to re-run (existing values are preserved).
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.Retrieval.VectorWeight',          N'0.65',          N'Decimal', N'Hybrid retrieval weight applied to passage-embedding cosine similarity. VectorWeight + KeywordWeight are normalized to sum to 1.0.'),
	(N'Decision.Retrieval.KeywordWeight',         N'0.35',          N'Decimal', N'Hybrid retrieval weight applied to keyword-overlap score. VectorWeight + KeywordWeight are normalized to sum to 1.0.'),
	(N'Decision.Retrieval.AuthoritativeBoost',    N'0.05',          N'Decimal', N'Additive rank bonus for decision-authoritative passages, applied after the vector/keyword blend.'),
	(N'Decision.Retrieval.VerifiedBoost',         N'0.05',          N'Decimal', N'Additive rank bonus for VERIFIED-evidence passages, applied after the vector/keyword blend.'),
	(N'Decision.Retrieval.InitialCandidateLimit', N'50',            N'Integer', N'Maximum candidate passages retrieved before reranking/truncation. Reserved for Phase E3 reranking.'),
	(N'Decision.Retrieval.RerankLimit',           N'10',            N'Integer', N'Maximum candidate passages kept after reranking. Reserved for Phase E3 reranking.'),
	(N'Decision.Retrieval.MinimumCandidateScore', N'0.00',          N'Decimal', N'Minimum retrieval rank required for a candidate passage to be retained.'),
	(N'Decision.Retrieval.FusionStrategy',        N'WeightedScore', N'String',  N'Hybrid fusion strategy: WeightedScore (production baseline) or ReciprocalRankFusion (Phase E3 benchmark scaffold, not yet wired into production).')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
