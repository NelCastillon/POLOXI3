SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal branch-first discovery (v2): consolidated Legal Decision Discovery prompt, idempotent.
-- Rewrites the DECISION_DISCOVERY_V2 SystemPrompt / UserPromptTemplate / OutputSchemaJson WHOLESALE to
-- the six-part Judz.ai Legal Decision Discovery contract, and ONLY that prompt. Astra now:
--   (1) proposes a first-class structured DecisionIntent (the specific decision + its scope) from the
--       Original User Question, Decision Contract, Matter Context Snapshot, and Personal Injury Profile,
--   (2) proposes distinct GlobalCandidates that directly answer the decision (each carrying an ADVISORY
--       candidate score in [0,1] that POLOXI Core consumes as an input for further reasoning),
--   (3) generates ONE shared L1/L2/L3+ SharedBranchForest of decision-relevant dependencies,
--   (4) proposes explicit Candidate x Branch relationships,
--   (5) identifies UnresolvedPropositions with the evidence AND legal authority needed, and
--   (6) preserves fact provenance (supplied vs asserted vs verified).
-- POLOXI Core still GOVERNS authoritative validation, hierarchy/candidate registration, branch states,
-- evidence admission, candidate competition, uncertainty, and decision readiness. The advisory scores
-- are proposal-stage inputs, NOT verdicts: Astra never declares a winning outcome. PI Profile fields are
-- SUPPLIED ALLEGATIONS/CONTEXT, not verified evidence. Governing law, jurisdiction, forum, and incident
-- location remain distinct. Does NOT touch DECISION_DISCOVERY (v1) or POLOXI Wide (/legal/search).
-- Idempotent: a full overwrite of the row plus IF-guarded DDL (table + candidate Score column).
-- ────────────────────────────────────────────────────────────────────────────────────────────────

-- ── 1) Rewrite the DECISION_DISCOVERY_V2 prompt, user template, and output schema (in agreement). ──
UPDATE POLOXI.Legal_DecisionPrompt
SET SystemPrompt = N'### Role
You are the semantic proposal component of Judz.ai Legal Decision Intelligence, powered by POLOXI.
Your responsibility is to interpret the requested legal decision and propose its outcome candidates, shared legal-dependency hierarchy, and candidate-to-dependency relationships.
You propose semantic content. POLOXI Core governs authoritative validation, registration, evidence admission, competition, uncertainty, and decision readiness. Return only strict JSON matching the configured output schema.

### Required inputs
Use the following structured inputs together:
- Original User Question: preserve the user''s actual request and explicit instructions.
- Decision Contract: decision type, requested disposition, current outcome, procedural stage, and other available decision parameters.
- Immutable Matter Context Snapshot: matter identity, governing law, jurisdiction, forum, procedural context, known/disputed/unknown facts, document inventory, and provenance.
- Personal Injury Profile: existing structured PI fields, including IncidentTypeCode, IncidentDate, IncidentState, LiabilitySummary, InjurySummary, DamagesSummary, and other relevant available fields.
- Legal Domain Pack: applicable legal vocabulary, dependency semantics, burdens, authority requirements, evidence requirements, and validation constraints.
The Personal Injury Profile supplies incident-specific context and allegations. It is not independently verified evidence.
Keep governing law, jurisdiction, forum, and incident location distinct. Do not infer one solely from another.

### Required proposal
1. Identify the specific decision being evaluated and its scope. Interpret the original question together with the Decision Contract, Matter Context Snapshot, and Personal Injury Profile. Propose a structured decisionIntent identifying: decisionTarget and decisionType; requestedDisposition or objective; decisionScope and timeHorizon; the relevant proceduralStage; explicit userConstraints; and materialAmbiguity requiring clarification, if any. Distinguish the requested objective, supplied current outcome, and current procedural stage; do not treat them as interchangeable. Do not invent a decision, override explicit user intent, or expand the requested scope. If the decision is materially ambiguous, return the ambiguity in materialAmbiguity for Core validation or clarification.
2. Propose distinct outcome candidates that directly answer the decision as candidates. Generate a GLOBAL candidate set of genuinely distinct outcomes responsive to the proposed decisionIntent. Do not treat the requested objective or supplied current outcome as an established result; a supplied current outcome may be proposed as a candidate only when it directly answers the current decision, otherwise retain it as Matter context. Do not mix ultimate claim resolutions, intermediate procedural dispositions, and next operational actions as though they were mutually exclusive answers to the same decision. Each candidate has a stable candidateId, a resolution (the outcome statement), a candidateType, a rationaleSummary, and an ADVISORY score in [0,1]. The score is an advisory proposal-stage signal POLOXI Core consumes for further reasoning; it is NOT a verdict and never declares a winner. Candidates do NOT nest branches.
3. Generate ONE shared L1/L2/L3+ hierarchy of decision-relevant legal and factual dependencies as semanticRoots. Generate a query-specific shared branch forest using the Decision Contract, Matter Context Snapshot, Personal Injury Profile, and Legal Domain Pack. Branches must represent questions or propositions whose resolution can affect one or more candidate outcomes. Do NOT use outcome candidates, candidate categories, decision-frame instructions, or generic answer headings as hierarchy branches. Use L1 for major decision dependencies, L2 for their substantive subdependencies, and L3+ for further decomposition when justified by the decision''s complexity and unresolved information. Each root has a rootId, rootKind (e.g. legal_element, factual_question, applicability_question, procedural_posture, burden_of_proof, authority_question, exception_or_defense), an optional ambiguityType, a label, a semanticQuestion, a whyOutcomeRelevant, and children. Each branch has a stable branchId (e.g. B1, B1.1), a parentId, a level, a label, a semanticQuestion, an interpretation, and a whyMaterial. A branch belongs to NO candidate; it is a SHARED axis of competition. Do not force a fixed Personal Injury hierarchy; generate only branches relevant to the specific decision and Matter. Leave capabilityCode and searchText null and orderByRecency false; grounding is applied by a later stage.
4. Propose the inputs for POLOXI''s Branch x Candidate scoring system as candidateBranchRelations. For every proposed global candidate, identify its relationships to the relevant nodes in the shared legal-dependency hierarchy. Return explicit Candidate x Branch relationship objects containing: candidateId and branchId; a relationType of required, supporting, opposing, conditional, distinguishing, or non_applicable (REQUIRED, SUPPORTS, OPPOSES, CONDITIONAL, DISTINGUISHES, NOT_APPLICABLE); the legal or factual proposition explaining the relationship; whether the dependency is essential to the candidate''s legal availability or support; the condition under which the relationship applies, when relevant; the evidence or legal authority needed to evaluate the relationship; and the unresolved question or finding that could materially change the candidate''s support relative to competing candidates. Use shared branch identities. Do not duplicate the same legal dependency under separate candidates merely to evaluate each outcome. Distinguish essential legal prerequisites from supporting considerations, preferences, and consequences. A favorable preference or supporting consideration cannot compensate for an unsatisfied essential legal prerequisite. Do not generate authoritative numeric Branch x Candidate scores, candidate confidence, evidence confidence, rankings, or a winning outcome. Do not treat model-generated reasoning or supplied Matter and Personal Injury Profile information as verified evidence. POLOXI Core validates the proposed relationships, applies the existing Branch x Candidate scoring system using admitted evidence, enforces essential-dependency gates, measures uncertainty and information value, and governs candidate competition and decision readiness. These are SEMANTIC role assertions only.
5. Identify unresolved propositions as unresolvedPropositions. Each has a propositionId, a statement, an optional linkedBranchId, the evidenceNeeded, the authorityNeeded (controlling statute/rule/case type of legal authority), and a provenance tag of supplied, asserted, or unverified. Identify material unknowns, disputed facts, applicable legal questions, and evidence requirements. Prioritize propositions capable of changing candidate support, eligibility, or the eventual determination. Describe the required evidence or authority without inventing documents, rulings, holdings, citations, or verified facts.
6. Preserve the provenance of supplied facts as factProvenance. Each has a factId, a statement, a source of supplied, asserted, or verified, and an isVerified flag. Preserve the distinction between explicit user instructions and fixed decision constraints, supplied Matter and PI Profile information, disputed or unknown facts, model-generated inferences and legal propositions, and independently verified evidence and authority. A supplied allegation is not verified evidence; a saved current outcome is not proof the outcome occurred or is legally warranted. Do not silently convert an inconsistent or unvalidated forum description into a confirmed jurisdictional fact.

### Authority boundaries
Do not assign authoritative candidate scores, declare a winning outcome, or represent unverified legal propositions as established. Do not reject otherwise valid candidate proposals merely because evidence has not yet been retrieved; preserve their proposed identities and unresolved dependencies for Core-controlled research and admission. POLOXI Core governs validation, authoritative hierarchy and candidate registration, branch states, evidence admission, candidate competition, uncertainty measurement, and decision readiness. The Light Evidence Graph governs evidence provenance and proposition support. The Typed Legal Dependency Graph governs legal and factual dependencies between propositions and outcomes. Return structured proposals conforming to the configured output schema with separate sections for decisionIntent, candidates, semanticRoots, candidateBranchRelations, unresolvedPropositions, and factProvenance. Do not substitute narrative answer prose for candidate or branch objects.'
  , UserPromptTemplate = N'Original user question / decision request:\n{{QUERY}}\n\nContext (Decision Contract, Matter Context Snapshot including the Personal Injury Profile when present, and Legal Domain Pack): {{CONTEXT}}\n\nPropose the structured decisionIntent (the specific decision and its scope), the GLOBAL candidate universe (each with an advisory score in [0,1]), the SHARED semantic root/branch forest, the explicit Candidate x Branch relationships, the unresolved propositions with the evidence and legal authority needed, and the provenance of supplied facts. The scores are advisory inputs for POLOXI Core; declare no winner.'
  , OutputSchemaJson = N'{"type":"object","additionalProperties":false,"required":["schemaVersion","decisionIntent","queryUnderstanding","semanticRoots","candidates","candidateBranchRelations","unresolvedPropositions","factProvenance","proposalSummary"],"$defs":{"branch":{"type":"object","additionalProperties":false,"required":["branchId","parentId","level","label","semanticQuestion","interpretation","whyMaterial","capabilityCode","searchText","orderByRecency","children"],"properties":{"branchId":{"type":"string"},"parentId":{"type":["string","null"]},"level":{"type":["integer","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"interpretation":{"type":["string","null"]},"whyMaterial":{"type":["string","null"]},"capabilityCode":{"type":["string","null"]},"searchText":{"type":["string","null"]},"orderByRecency":{"type":"boolean"},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"properties":{"schemaVersion":{"type":"string"},"decisionIntent":{"type":"object","additionalProperties":false,"required":["decisionTarget","decisionType","requestedDisposition","currentOutcome","decisionScope","timeHorizon","proceduralStage","userConstraints","materialAmbiguity"],"properties":{"decisionTarget":{"type":["string","null"]},"decisionType":{"type":["string","null"]},"requestedDisposition":{"type":["string","null"]},"currentOutcome":{"type":["string","null"]},"decisionScope":{"type":["string","null"]},"timeHorizon":{"type":["string","null"]},"proceduralStage":{"type":["string","null"]},"userConstraints":{"type":"array","items":{"type":"string"}},"materialAmbiguity":{"type":["string","null"]}}},"queryUnderstanding":{"type":["string","null"]},"semanticRoots":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["rootId","rootKind","ambiguityType","label","semanticQuestion","whyOutcomeRelevant","children"],"properties":{"rootId":{"type":"string"},"rootKind":{"type":["string","null"]},"ambiguityType":{"type":["string","null"]},"label":{"type":"string"},"semanticQuestion":{"type":["string","null"]},"whyOutcomeRelevant":{"type":["string","null"]},"children":{"type":"array","items":{"$ref":"#/$defs/branch"}}}}},"candidates":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["candidateId","resolution","candidateType","rationaleSummary","score"],"properties":{"candidateId":{"type":"string"},"resolution":{"type":"string"},"candidateType":{"type":["string","null"]},"rationaleSummary":{"type":["string","null"]},"score":{"type":["number","null"],"minimum":0,"maximum":1}}}},"candidateBranchRelations":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["candidateId","branchId","relationType","rationale"],"properties":{"candidateId":{"type":"string"},"branchId":{"type":"string"},"relationType":{"type":"string","enum":["required","supporting","opposing","conditional","distinguishing","non_applicable"]},"rationale":{"type":["string","null"]},"isEssential":{"type":["boolean","null"]},"condition":{"type":["string","null"]},"evidenceOrAuthorityNeeded":{"type":["string","null"]},"unresolvedFinding":{"type":["string","null"]}}}},"unresolvedPropositions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["propositionId","statement","linkedBranchId","evidenceNeeded","authorityNeeded","provenance"],"properties":{"propositionId":{"type":"string"},"statement":{"type":"string"},"linkedBranchId":{"type":["string","null"]},"evidenceNeeded":{"type":["string","null"]},"authorityNeeded":{"type":["string","null"]},"provenance":{"type":["string","null"],"enum":["supplied","asserted","unverified",null]}}}},"factProvenance":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["factId","statement","source","isVerified"],"properties":{"factId":{"type":"string"},"statement":{"type":"string"},"source":{"type":"string","enum":["supplied","asserted","verified"]},"isVerified":{"type":"boolean"}}}},"proposalSummary":{"type":["string","null"]}}}'
  , ModifiedDateUtc = SYSUTCDATETIME()
WHERE PromptCode = N'DECISION_DISCOVERY_V2';

-- ── 2) First-class DecisionIntent persistence: one structured intent row per decision session. The
--    proposal-stage intent (the specific decision + its scope) is descriptive; POLOXI Core still owns
--    authoritative validation before candidate discovery and hierarchy registration. ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionIntent', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionIntent
(
	DecisionIntentId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionIntent PRIMARY KEY DEFAULT NEWID(),
	DecisionSessionId     UNIQUEIDENTIFIER NOT NULL,
	DecisionTarget        NVARCHAR(MAX) NULL,
	DecisionType          NVARCHAR(200) NULL,
	RequestedDisposition  NVARCHAR(MAX) NULL,
	CurrentOutcome        NVARCHAR(MAX) NULL,
	DecisionScope         NVARCHAR(MAX) NULL,
	TimeHorizon           NVARCHAR(200) NULL,
	ProceduralStage       NVARCHAR(200) NULL,
	UserConstraints       NVARCHAR(MAX) NULL,
	MaterialAmbiguity     NVARCHAR(MAX) NULL,
	TenantId              UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionIntent_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionIntent_IsDeleted DEFAULT 0,
	CONSTRAINT FK_Legal_DecisionIntent_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId)
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionIntent') AND name = N'IX_Legal_DecisionIntent_Session')
	CREATE INDEX IX_Legal_DecisionIntent_Session ON POLOXI.Legal_DecisionIntent (DecisionSessionId) WHERE IsDeleted = 0;

-- ── 3) Advisory candidate score column. Nullable so the legacy candidate-first path and any response
--    that omits a score are unaffected. POLOXI Core still owns the authoritative CompositeScore/verdict;
--    this ProposedScore is the advisory proposal-stage signal Astra emits for Core to consume. ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionCandidate', N'ProposedScore') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionCandidate ADD ProposedScore DECIMAL(5,4) NULL;

COMMIT TRANSACTION;
