SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI Premium (LIGHT scaffolding) discovery prompt, version 2.
-- Supersedes the v1 LIGHT discovery prompt via VersionNumber ordering; v1 remains for rollback.
-- Premium models need minimal scaffolding: intent, requirements, per-node fields, and a
-- self-verification gate. The output contract remains the canonical flat-node schema enforced
-- by the strict json_schema response format.
IF NOT EXISTS(SELECT 1 FROM POLOXI.PromptStrategy WHERE TenantId IS NULL AND PurposeCode=N'AMBIGUITY_DISCOVERY' AND ScaffoldingCode=N'LIGHT' AND VersionNumber=2 AND IsDeleted=0)
BEGIN
	INSERT POLOXI.PromptStrategy(TenantId,PurposeCode,ScaffoldingCode,VersionNumber,SystemPrompt,UserPromptTemplate,SortOrder)
	VALUES
	(NULL,N'AMBIGUITY_DISCOVERY',N'LIGHT',2,
N'You are POLOXI Ambiguity Discovery. You analyze a user request and return a recursive semantic hierarchy of every materially decision-relevant ambiguity. You never answer the user''s original request. You return strictly valid JSON matching the supplied schema and nothing else.',
N'Analyze the user''s request for all materially decision-relevant ambiguities.

Construct a recursive semantic hierarchy: Root (depth 0) -> Ambiguity nodes (depth 1) -> Interpretation nodes -> Dimension/SubDimension nodes -> EvidenceLeaf nodes.

Requirements:
1. Identify all independent and interacting material ambiguities.
2. Preserve competing interpretations rather than prematurely selecting one.
3. Recursively decompose each interpretation into semantically narrower, operationally distinct dimensions.
4. Continue each branch only until it reaches an evidence-ready, measurable or externally verifiable leaf (maximum depth {MaxDepth}).
5. Do not force equal depth or equal child counts.
6. Do not create synonyms, redundant branches, or artificial depth.
7. Identify dependencies between ambiguities and record them in the separate dependencies list (DependsOn, Modifies, Constrains, Overlaps, ConflictsWith, ProvidesContextFor), never by merging tree nodes.
8. Do not answer the original question.

For each node return: id, parentId (null only for the Root), depth (Root = 0, child depth = parent depth + 1), name, sourceText, nodeType, ambiguityType, materiality, decisionRole, operationalDefinition, metricOrObservation, evidenceNeeded, evidenceType, preferenceDirection, isLeaf, proposedConfidence.

Record borderline non-material ambiguities in excludedAmbiguities with reasons instead of expanding them.

Before returning, verify:
- all material ambiguities were captured;
- parent-child relationships are valid;
- siblings are materially distinct;
- leaves are evidence-ready with evidenceNeeded populated;
- no branch was prematurely collapsed;
and record the verification in the audit object.

Return according to the supplied hierarchy JSON schema.

ORIGINAL REQUEST:
{Query}',5);
END;

-- Capability coverage for GPT-5.6 "Sol" model codes that do not contain "gpt-5" in the routed
-- ModelCode (e.g. codes exposed as "sol" variants). Existing %gpt-5% pattern already covers
-- gpt-5.6 codes; this row only catches sol-branded codes ahead of the STANDARD fallback.
IF NOT EXISTS(SELECT 1 FROM POLOXI.ModelCapabilityProfile WHERE TenantId IS NULL AND ModelCodePattern=N'%sol%' AND IsDeleted=0)
INSERT POLOXI.ModelCapabilityProfile(TenantId,ModelCodePattern,TierCode,SemanticReasoning,MultiAmbiguityRecall,RecursiveDecomposition,StructuralReliability,InstructionFollowing,CostScore,LatencyScore,RecommendedMaxDepth,RecommendedScaffoldingCode,SortOrder)
VALUES(NULL,N'%sol%',N'PREMIUM',0.95,0.92,0.94,0.95,0.95,0.28,0.38,10,N'LIGHT',35);

COMMIT TRANSACTION;
