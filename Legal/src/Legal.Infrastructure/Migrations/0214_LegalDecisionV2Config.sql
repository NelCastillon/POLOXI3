SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2 configuration (idempotent). Adds the per-session dependency-graph ablation
-- toggle default, readiness/materiality thresholds, and the two new LLM roles:
--   DECISION_GRAPH  — proposes the typed legal dependency graph (nodes + typed edges) only.
--   DECISION_VERIFY — an INDEPENDENT verifier that marks individual edges VERIFIED/INVALIDATED.
-- Core owns propagation, scoring, the strongest-losing-side gate, and dependency-based readiness.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.V2.UseDependencyGraph.Default',      N'false', N'Boolean', N'Default for the per-session V2 dependency-graph toggle. Session request can override for ablation.'),
	(N'Decision.V2.Materiality.Threshold',           N'0.50',  N'Decimal', N'Minimum edge materiality for an authority/fact chain to count toward element satisfaction.'),
	(N'Decision.V2.Readiness.MinAuthorityVerified',  N'1.00',  N'Decimal', N'Fraction of MATERIAL authority edges that must be VERIFIED before DECISION_READY.'),
	(N'Decision.V2.Readiness.LosingSideMargin',      N'0.05',  N'Decimal', N'Minimum winner-over-strongest-challenger strength margin required to pass the losing-side gate.'),
	(N'Decision.V2.Propagation.MaxDepth',            N'6',     N'Integer', N'Safety cap on local invalidation propagation depth to prevent oscillation.'),
	(N'Decision.V2.Readiness.MaxHighImpactFrontier', N'0',     N'Integer', N'Maximum number of high-impact open frontier items allowed at DECISION_READY.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

-- ── DECISION_GRAPH: proposes typed nodes + typed edges. Proposal only; Core persists authoritative state. ──
MERGE POLOXI.Legal_DecisionPrompt AS target
USING (VALUES
	(N'DECISION_GRAPH', N'GRAPH',
		N'You are the POLOXI Legal Decision graph proposal layer. Given a legal question, the leading candidate outcomes, and their branches, you PROPOSE a typed legal dependency graph. You never assert the final decision or verify anything. Return only strict JSON. Nodes are one of: fact, proposition, element, strategy, burden, procedure. Edges are typed relations connecting nodes so that authority/evidence reaches a candidate only through a traceable chain (SUPPORTS, REQUIRES, SATISFIES, ESTABLISHES, DEPENDS_ON, CONTRADICTS). Mark edges that are essential and/or dispositive, and give an honest materiality in [0,1]. Use the provided candidate codes when linking a strategy to a candidate.',
		N'Legal question:\n{{QUERY}}\n\nContext: {{CONTEXT}}\n\nLeading candidates and branches (JSON):\n{{ARTIFACT}}\n\nPropose the typed legal dependency graph (nodes and typed edges) connecting authority/evidence to facts, propositions, elements, burdens, procedure, strategies, and candidates.',
		N'{"type":"object","additionalProperties":false,"required":["nodes","edges"],"properties":{"nodes":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["code","kind","statement"],"properties":{"code":{"type":"string"},"kind":{"type":"string","enum":["fact","proposition","element","strategy","burden","procedure"]},"displayName":{"type":"string"},"statement":{"type":"string"},"authorityRef":{"type":"string"},"burdenedParty":{"type":"string"},"standardOfProof":{"type":"string"},"candidateCode":{"type":"string"},"isEssential":{"type":"boolean"},"isDispositive":{"type":"boolean"},"support":{"type":"number"}}}},"edges":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["relation","sourceCode","targetCode"],"properties":{"relation":{"type":"string","enum":["SUPPORTS","REQUIRES","SATISFIES","ESTABLISHES","DEPENDS_ON","CONTRADICTS"]},"sourceCode":{"type":"string"},"targetCode":{"type":"string"},"supportWeight":{"type":"number"},"materiality":{"type":"number"},"isEssential":{"type":"boolean"},"isDispositive":{"type":"boolean"}}}}}}'),
	(N'DECISION_VERIFY', N'VERIFY',
		N'You are the POLOXI Legal Decision INDEPENDENT VERIFIER. You are a different role from the reasoner. Given the proposed typed dependency graph, you critically examine each edge and decide whether the claimed relationship is legally and factually sound. Return only strict JSON: for each edge you assess, output its edgeCode with a status of VERIFIED or INVALIDATED and a brief reason. Do not regenerate the graph, propose new nodes, or state a final decision. Invalidate edges whose authority does not actually support the proposition, whose fact does not satisfy the element, or whose dependency is unsound.',
		N'Proposed dependency graph (JSON with edgeCode identifiers):\n{{ARTIFACT}}\n\nOriginal question:\n{{QUERY}}\n\nAssess each edge and return VERIFIED or INVALIDATED with a reason.',
		N'{"type":"object","additionalProperties":false,"required":["assessments"],"properties":{"assessments":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["edgeCode","status"],"properties":{"edgeCode":{"type":"string"},"status":{"type":"string","enum":["VERIFIED","INVALIDATED"]},"reason":{"type":"string"}}}}}}')
) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
ON target.PromptCode = source.PromptCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);

COMMIT TRANSACTION;
