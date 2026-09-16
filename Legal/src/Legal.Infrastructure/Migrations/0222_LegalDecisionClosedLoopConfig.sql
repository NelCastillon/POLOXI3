SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2.1 — Closed-loop feature flags + loop-safety settings (idempotent).
--
-- These control the closed loop: verification change → propagation → domain-neutral branch signals
-- → POLOXI Candidate×Branch recompetition → frontier/IV recalculation → ResearchNeed → readiness.
-- POLOXI stays authoritative; these flags only enable/disable graph-derived SIGNALS feeding it.
-- Per the confirmed scope, all V2.1 feature flags default ON. Loop-safety limits bound the loop.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.V2.UseDependencyPropagation',       N'true', N'Boolean', N'Enable deterministic dependency propagation producing a structured DependencyImpact when an edge verification changes.'),
	(N'Decision.V2.UseGraphDrivenRecompetition',    N'true', N'Boolean', N'Allow graph-derived domain-neutral signals to trigger POLOXI Candidate x Branch recompetition. POLOXI remains the sole scorer.'),
	(N'Decision.V2.UseGraphFrontierSignals',        N'true', N'Boolean', N'Feed dependency-derived signals into decision-frontier and Information Value recalculation.'),
	(N'Decision.V2.Loop.MaxReopensPerBranch',       N'3',    N'Integer', N'Maximum times a single branch may be reopened by the closed loop before it is held to prevent oscillation.'),
	(N'Decision.V2.Loop.MaxResearchActions',        N'8',    N'Integer', N'Maximum outcome-directed ResearchNeed actions generated per session by the closed loop.'),
	(N'Decision.V2.Loop.NoInformationGainEpsilon',  N'0.01', N'Decimal', N'Minimum entropy/IV change required for the loop to treat a recompetition as producing information gain.'),
	(N'Decision.V2.Benchmark.Enabled',              N'true', N'Boolean', N'Persist benchmark-compatible closed-loop telemetry (reopens, propagation events, rank changes, latency) for ablation.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
