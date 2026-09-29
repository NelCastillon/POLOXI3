SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Attorney Decision Input (ADI) / "Human Intelligence" feature flags (§27). All OFF by default; the
-- read-only Human Intelligence tab surfaces DB-backed data but write/preview/commit/scoring paths stay
-- disabled until deliberately enabled per tenant/matter. Idempotent MERGE — safe to re-run.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'AttorneyDecisionInput.Enabled',           N'false', N'Boolean', N'Master switch for the Attorney Decision Input (Human Intelligence) surface. OFF by default.'),
	(N'AttorneyRelativeAssessment.Enabled',       N'false', N'Boolean', N'Enables attorney relative-value assessments on decision nodes.'),
	(N'AttorneyAssessmentAdjustment.Enabled',     N'false', N'Boolean', N'Allows attorneys to deliberately adjust the suggested midpoint within the neighboring range.'),
	(N'AttorneyMultiAssessment.Enabled',          N'false', N'Boolean', N'Allows multiple attorneys to independently assess one canonical node.'),
	(N'AttorneyAssessmentApproval.Enabled',       N'false', N'Boolean', N'Enables governance approval of a single active matter assessment per node.'),
	(N'AttorneyChallenge.Enabled',                N'false', N'Boolean', N'Enables attorney challenges against nodes/relationships.'),
	(N'AttorneyReposition.Enabled',               N'false', N'Boolean', N'Enables attorney repositioning of nodes (creates a new version and DecisionDelta).'),
	(N'AttorneyDecisionPreview.Enabled',          N'false', N'Boolean', N'Enables non-mutating preview through the existing POLOXI scoring path.'),
	(N'AttorneyInputCdc.Enabled',                 N'false', N'Boolean', N'Enables CDC/outbox transport for attorney decision input events.'),
	(N'AttorneyInputCdi.Enabled',                 N'false', N'Boolean', N'Enables CDI decision-change explanations for attorney inputs.'),
	(N'AprForAttorneyInput.Enabled',              N'false', N'Boolean', N'Enables Adaptive Proposition Resolver checks for attorney-supplied nodes.'),
	(N'CoverageOnAttorneyMutation.Enabled',       N'false', N'Boolean', N'Enables coverage rechecks after attorney hierarchy mutations.'),
	(N'LightGraphAttorneyEdges.Enabled',          N'false', N'Boolean', N'Enables light evidence-graph typed edges for attorney-created nodes.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
