SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal branch-first discovery (v2), idempotent. Introduces DECISION_DISCOVERY_V2 alongside
-- the existing candidate-first DECISION_DISCOVERY (which is left untouched for rollback). v2 mirrors
-- the /legal/search Wide semantic pipeline (IntelligenceWideService WIDE_SEMANTIC_PROPOSAL): the LLM
-- proposes a SHARED semantic root/branch forest (branches own NO candidate) plus ONE global candidate
-- universe carrying NO scores. POLOXI Core owns all scoring — it competes every candidate against the
-- shared branch forest and derives each composite, the ranking, and the flip points from RETRIEVED
-- EVIDENCE, never from LLM-supplied numbers. The feature flag defaults OFF so the legacy candidate-
-- first path stays byte-identical until a tenant opts in.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'Decision.Discovery.BranchFirst.Enabled', N'false', N'Boolean', N'When true, the legal discovery stage uses DECISION_DISCOVERY_V2 (a shared semantic root/branch forest + a scoreless global candidate universe, mirroring the /legal/search Wide pipeline; POLOXI Core derives all scoring from retrieved evidence). When false, the legacy candidate-first DECISION_DISCOVERY path runs. Default false for rollback safety.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

-- ── DECISION_DISCOVERY_V2: branch-first proposal mirroring the /legal/search Wide semantic pipeline. ──
--    Emits a SHARED semanticRoots tree (branches own NO candidate) + a MINIMAL global candidate
--    universe (NO scores). POLOXI Core derives every candidate composite, ranking, and flip point
--    downstream from retrieved evidence per branch — identical to the Wide WIDE_SEMANTIC_PROPOSAL
--    contract. The schema is field-compatible with SemanticProposalResult so the same parser applies.
MERGE POLOXI.Legal_DecisionPrompt AS target
USING (VALUES
	(N'DECISION_DISCOVERY_V2', N'DISCOVERY',
		N'You are the POLOXI Legal Decision proposal layer (branch-first, semantic). You transform a legal question into a high-recall, minimum-sufficient semantic decision space that POLOXI Core governs. You NEVER assert the final decision, NEVER rank the candidates, and NEVER emit any numeric score — POLOXI Core owns all scoring and ranking and derives them downstream from retrieved evidence. Return only strict JSON matching the schema. Produce a SHARED, decision-independent semantic forest and ONE global candidate universe. (1) semanticRoots: the materially independent Level-1 (L1) dimensions that determine the outcome. Discover breadth before depth — surface all independent roots first, each with a rootKind (e.g. legal_element, factual_question, applicability_question, procedural_posture, burden_of_proof, authority_question, exception_or_defense) and an ambiguityType where relevant. Each root has a stable rootId, a label, a semanticQuestion, a whyOutcomeRelevant, and children. Decompose a root vertically into child branches (L2/L3) ONLY when a child materially changes interpretation, candidate viability, a discriminator, an evidence need, or the outcome. Each branch has a stable branchId (e.g. B1, B1.1, B1.1.1), a parentId, a level, a label, a semanticQuestion, an interpretation, a whyMaterial, and children. A branch belongs to NO candidate — it is a SHARED axis of competition every candidate is evaluated against. Leave capabilityCode and searchText null and orderByRecency false; grounding is applied by a later stage. (2) candidates: ONE global universe of the materially distinct competing legal outcomes, each with a stable candidateId, a resolution (the outcome statement), a candidateType, and a rationaleSummary. Candidates carry NO scores and do NOT nest branches. Provide broad, well-differentiated coverage of the plausible outcomes so Core can compete them against the shared branch forest using evidence.',
		N'Legal question / decision request:\n{{QUERY}}\n\nContext: {{CONTEXT}}\n\nPropose the SHARED semantic root/branch forest and the GLOBAL candidate universe. Emit no scores.',
		N'{"type":"object","additionalProperties":false,"required":["schemaVersion","queryUnderstanding","semanticRoots","candidates","proposalSummary"],"$defs":{"branch":{"type":"object","additionalProperties":false,"required":["branchId","parentId","level","label","semanticQuestion","interpretation","whyMaterial","capabilityCode","searchText","orderByRecency","children"],"properties":{"branchId":{"type":"string"},"parentId":{"type":["string","null"]},"level":{"type":["integer","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"interpretation":{"type":["string","null"]},"whyMaterial":{"type":["string","null"]},"capabilityCode":{"type":["string","null"]},"searchText":{"type":["string","null"]},"orderByRecency":{"type":"boolean"},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"properties":{"schemaVersion":{"type":"string"},"queryUnderstanding":{"type":["string","null"]},"semanticRoots":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["rootId","rootKind","ambiguityType","label","semanticQuestion","whyOutcomeRelevant","children"],"properties":{"rootId":{"type":"string"},"rootKind":{"type":["string","null"]},"ambiguityType":{"type":["string","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"whyOutcomeRelevant":{"type":["string","null"]},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"candidates":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["candidateId","resolution","candidateType","rationaleSummary"],"properties":{"candidateId":{"type":"string"},"resolution":{"type":"string"},"candidateType":{"type":["string","null"]},"rationaleSummary":{"type":["string","null"]}}}},"proposalSummary":{"type":["string","null"]}}}')
) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
ON target.PromptCode = source.PromptCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);

-- Route DECISION_DISCOVERY_V2 through the SAME model as the v1 DECISION_DISCOVERY feature (Astra),
-- so the branch-first proposal has the same reasoning budget as the /legal/search Wide pipeline.
-- Idempotent; no-op if a matching (FeatureCode, ModelCode, tenant-null, not-deleted) route exists.
MERGE POLOXI.Legal_DecisionModelRoute AS target
USING (VALUES
	(N'DECISION_DISCOVERY_V2', N'AZURE_OPENAI', N'gpt-6-astra', N'gpt-6-astra', N'env://AMS_AZURE_OPENAI_ENDPOINT', N'env://AMS_AZURE_OPENAI_KEY', N'2024-10-21', 600, 65536, CAST(0.00 AS DECIMAL(4,2)), 10)
) AS source (FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference, CredentialReference, ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority)
ON target.FeatureCode = source.FeatureCode
	AND target.ModelCode = source.ModelCode
	AND target.TenantId IS NULL
	AND target.IsDeleted = 0
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference, CredentialReference, ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority)
	VALUES (source.FeatureCode, source.ProviderTypeCode, source.ModelCode, source.DeploymentName, source.EndpointReference, source.CredentialReference, source.ApiVersion, source.TimeoutSeconds, source.MaxOutputTokens, source.Temperature, source.Priority);

COMMIT TRANSACTION;
