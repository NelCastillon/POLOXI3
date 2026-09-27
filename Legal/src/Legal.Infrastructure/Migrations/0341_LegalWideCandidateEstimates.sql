-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- 0341: Wide candidate advisory estimates — Estimated time-to-resolution & cost band (Table stage).
--
-- The Decision Landscape (Overview) tab compares the top outcomes side-by-side. In addition to the
-- support/dependency/evidence metrics already persisted, the pipeline may produce two advisory,
-- model-generated estimates per candidate outcome:
--   • EstimatedResolutionLabel — a coarse time-to-resolution band (e.g. N'3–6 months').
--   • EstimatedCostBand        — a coarse relative cost band (e.g. N'Lower', N'Higher').
--
-- Both are OPTIONAL and NULLABLE. They are advisory only: when the model does not return them the
-- application shows an em dash and never fabricates a value. Non-legal Wide runs simply leave them NULL.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_WideCandidate', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_WideCandidate', N'EstimatedResolutionLabel') IS NULL
		ALTER TABLE POLOXI.Legal_WideCandidate ADD EstimatedResolutionLabel NVARCHAR(60) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_WideCandidate', N'EstimatedCostBand') IS NULL
		ALTER TABLE POLOXI.Legal_WideCandidate ADD EstimatedCostBand NVARCHAR(40) NULL;
END;
