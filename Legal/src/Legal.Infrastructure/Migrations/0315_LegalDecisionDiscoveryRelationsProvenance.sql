SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal branch-first discovery (v2) enrichment, idempotent. Extends DECISION_DISCOVERY_V2 —
-- and ONLY that prompt — so Astra additionally proposes:
--   (4) explicit Candidate × Branch relationships (required / supporting / opposing / conditional /
--       distinguishing / non_applicable) against the SHARED branch forest,
--   (5) unresolved propositions with the evidence AND legal authority needed to resolve each, and
--   (6) fact provenance separating SUPPLIED facts from independently VERIFIED evidence.
-- POLOXI Core still owns ALL scoring, evidence admission, candidate competition, uncertainty, and
-- decision readiness. The LLM emits NO numeric scores and declares NO winning outcome. This migration
-- does NOT touch DECISION_DISCOVERY (v1) or the general-purpose POLOXI Wide (/legal/search) engine.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

-- ── 1) Update the DECISION_DISCOVERY_V2 prompt, user template, and output schema (in agreement). ──
UPDATE POLOXI.Legal_DecisionPrompt
SET SystemPrompt = N'You are the POLOXI Legal Decision proposal layer (branch-first, semantic). You transform a legal question into a high-recall, minimum-sufficient semantic decision space that POLOXI Core governs. You NEVER assert the final decision, NEVER declare a winning outcome, NEVER rank candidates, and NEVER emit any numeric score — POLOXI Core owns all scoring, evidence admission, candidate competition, uncertainty, and decision readiness, and derives them downstream from retrieved evidence. Return only strict JSON matching the schema.

Given the Decision Contract, Matter Context Snapshot, and applicable Legal Domain Pack:
1. Identify the specific decision being evaluated and its scope; state it in queryUnderstanding.
2. Propose distinct outcome candidates that directly answer that decision. Do NOT treat the requested objective or any supplied current outcome as an established result. Emit them as ONE global candidate universe: each candidate has a stable candidateId, a resolution (the outcome statement), a candidateType, and a rationaleSummary. Candidates carry NO scores and do NOT nest branches.
3. Generate ONE shared L1/L2/L3+ hierarchy of decision-relevant legal and factual dependencies as semanticRoots. Do NOT use outcome candidates or decision-frame instructions as hierarchy branches. Discover breadth before depth: surface all materially independent L1 roots first, each with a rootId, a rootKind (e.g. legal_element, factual_question, applicability_question, procedural_posture, burden_of_proof, authority_question, exception_or_defense), an optional ambiguityType, a label, a semanticQuestion, a whyOutcomeRelevant, and children. Decompose a root vertically into child branches (L2/L3) ONLY when a child materially changes interpretation, candidate viability, a discriminator, an evidence need, or the outcome. Each branch has a stable branchId (e.g. B1, B1.1, B1.1.1), a parentId, a level, a label, a semanticQuestion, an interpretation, and a whyMaterial. A branch belongs to NO candidate — it is a SHARED axis of competition every candidate is evaluated against. Leave capabilityCode and searchText null and orderByRecency false; grounding is applied by a later stage.
4. Propose explicit Candidate × Branch relationships as candidateBranchRelations. Each links one candidateId to one branchId with a relationType of required, supporting, opposing, conditional, distinguishing, or non_applicable, plus a short rationale. These are SEMANTIC role assertions only, never scores.
5. Identify unresolved propositions as unresolvedPropositions. Each has a propositionId, a statement, an optional linkedBranchId, the evidenceNeeded to evaluate it, the authorityNeeded (controlling statute/rule/case type of legal authority), and a provenance tag of supplied, asserted, or unverified.
6. Preserve the provenance of supplied facts as factProvenance. Each has a factId, a statement, a source of supplied (given in the contract/matter), asserted (stated by a party but unproven), or verified (independently corroborated), and an isVerified flag. Distinguish supplied facts from independently verified evidence; do not represent unverified legal propositions or asserted facts as established.

Produce a SHARED, decision-independent semantic forest and ONE global candidate universe. POLOXI Core competes every candidate against the shared branch forest using evidence.'
  , UserPromptTemplate = N'Legal question / decision request:\n{{QUERY}}\n\nContext: {{CONTEXT}}\n\nPropose the SHARED semantic root/branch forest, the GLOBAL candidate universe, the explicit Candidate × Branch relationships, the unresolved propositions with the evidence and legal authority needed, and the provenance of supplied facts. Emit no scores and declare no winner.'
  , OutputSchemaJson = N'{"type":"object","additionalProperties":false,"required":["schemaVersion","queryUnderstanding","semanticRoots","candidates","candidateBranchRelations","unresolvedPropositions","factProvenance","proposalSummary"],"$defs":{"branch":{"type":"object","additionalProperties":false,"required":["branchId","parentId","level","label","semanticQuestion","interpretation","whyMaterial","capabilityCode","searchText","orderByRecency","children"],"properties":{"branchId":{"type":"string"},"parentId":{"type":["string","null"]},"level":{"type":["integer","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"interpretation":{"type":["string","null"]},"whyMaterial":{"type":["string","null"]},"capabilityCode":{"type":["string","null"]},"searchText":{"type":["string","null"]},"orderByRecency":{"type":"boolean"},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"properties":{"schemaVersion":{"type":"string"},"queryUnderstanding":{"type":["string","null"]},"semanticRoots":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["rootId","rootKind","ambiguityType","label","semanticQuestion","whyOutcomeRelevant","children"],"properties":{"rootId":{"type":"string"},"rootKind":{"type":["string","null"]},"ambiguityType":{"type":["string","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"whyOutcomeRelevant":{"type":["string","null"]},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"candidates":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["candidateId","resolution","candidateType","rationaleSummary"],"properties":{"candidateId":{"type":"string"},"resolution":{"type":"string"},"candidateType":{"type":["string","null"]},"rationaleSummary":{"type":["string","null"]}}}},"candidateBranchRelations":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["candidateId","branchId","relationType","rationale"],"properties":{"candidateId":{"type":"string"},"branchId":{"type":"string"},"relationType":{"type":"string","enum":["required","supporting","opposing","conditional","distinguishing","non_applicable"]},"rationale":{"type":["string","null"]}}}},"unresolvedPropositions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["propositionId","statement","linkedBranchId","evidenceNeeded","authorityNeeded","provenance"],"properties":{"propositionId":{"type":"string"},"statement":{"type":"string"},"linkedBranchId":{"type":["string","null"]},"evidenceNeeded":{"type":["string","null"]},"authorityNeeded":{"type":["string","null"]},"provenance":{"type":["string","null"],"enum":["supplied","asserted","unverified",null]}}}},"factProvenance":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["factId","statement","source","isVerified"],"properties":{"factId":{"type":"string"},"statement":{"type":"string"},"source":{"type":"string","enum":["supplied","asserted","verified"]},"isVerified":{"type":"boolean"}}}},"proposalSummary":{"type":["string","null"]}}}'
  , ModifiedDateUtc = SYSUTCDATETIME()
WHERE PromptCode = N'DECISION_DISCOVERY_V2';

-- ── 2) Candidate × Branch relationship edges (§4). A branch is a SHARED axis of competition; this
--    table records the SEMANTIC role each candidate plays against each shared branch. No scores. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionCandidateBranchRelation', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionCandidateBranchRelation
(
	DecisionCandidateBranchRelationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionCandidateBranchRelation PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId    UNIQUEIDENTIFIER NOT NULL,
	DecisionCandidateId  UNIQUEIDENTIFIER NOT NULL,
	DecisionBranchId     UNIQUEIDENTIFIER NOT NULL,
	RelationTypeCode     NVARCHAR(30) NOT NULL,
	Rationale            NVARCHAR(MAX) NULL,
	TenantId             UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionCandBranchRel_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc      DATETIME2 NULL,
	ModifiedByUserId     UNIQUEIDENTIFIER NULL,
	IsDeleted            BIT NOT NULL CONSTRAINT DF_Legal_DecisionCandBranchRel_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionCandBranchRel_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF OBJECT_ID(N'IX_Legal_DecisionCandBranchRel_Session', N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionCandBranchRel_Session ON POLOXI.Legal_DecisionCandidateBranchRelation (DecisionSessionId, DecisionCandidateId) WHERE IsDeleted = 0;

-- ── 3) Provenance / research-need typing on the dependency graph (§5,§6). Unresolved propositions and
--    supplied-vs-verified facts are persisted as typed Legal_DecisionDependency nodes; these columns
--    carry the additional provenance/authority metadata the enriched proposal supplies. ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionDependency', N'ProvenanceCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDependency ADD ProvenanceCode NVARCHAR(30) NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionDependency', N'EvidenceNeeded') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDependency ADD EvidenceNeeded NVARCHAR(MAX) NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionDependency', N'AuthorityNeeded') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDependency ADD AuthorityNeeded NVARCHAR(MAX) NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionDependency', N'IsVerified') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDependency ADD IsVerified BIT NOT NULL CONSTRAINT DF_Legal_DecisionDependency_IsVerified DEFAULT 0;

IF COL_LENGTH(N'POLOXI.Legal_DecisionDependency', N'LinkedBranchCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDependency ADD LinkedBranchCode NVARCHAR(60) NULL;

COMMIT TRANSACTION;
