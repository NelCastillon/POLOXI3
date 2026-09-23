SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'ResearchQuestion') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD ResearchQuestion NVARCHAR(MAX) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'SearchQuery') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD SearchQuery NVARCHAR(MAX) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'SearchConceptsJson') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD SearchConceptsJson NVARCHAR(MAX) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'AuthorityKindsJson') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD AuthorityKindsJson NVARCHAR(MAX) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'ApplicationDeferred') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD ApplicationDeferred BIT NOT NULL
			CONSTRAINT DF_Legal_DecisionResearchNeed_ApplicationDeferred DEFAULT 0;
END

IF OBJECT_ID(N'POLOXI.Legal_DecisionPrompt', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt = N'You are performing POLOXI Search-style semantic decomposition for an unresolved decision frontier. Do not answer or resolve the legal question. Produce a hierarchy of independently resolvable leaves before application. Keep four artifacts distinct: the frontier uncertainty, a research question, a declarative researchable proposition, and a provider-facing search query. A research question asks what must be learned. A proposition is a declarative statement that the assigned source can directly support, contradict, or leave unresolved; it must never be phrased as a question. A search query is retrieval syntax and must not become the proposition verified against evidence. Separate governing legal rule, controlling legal authority, procedural standard, matter fact, matter evidence, and application. Public legal authority cannot establish whether evidence or factual conflict exists in this matter. Matter facts and evidence route only to MATTER_DOCUMENT. APPLICATION and DERIVED nodes use sourceClass NONE, researchable false, applicationDeferred true, no search query/concepts/authority kinds, and dependencies on prerequisite leaves. Return only strict JSON matching the schema.',
		UserPromptTemplate = N'ORIGINAL QUESTION:\n{{QUERY}}\n\nCURRENT CANDIDATES:\n{{CANDIDATES}}\n\nFRONTIER BRANCH:\n{{FRONTIER}}\n\nCreate at least three leaves and at least two independently researchable leaves. For each researchable leaf provide: (1) researchQuestion, (2) a declarative proposition, (3) sourceClass, (4) concise provider-facing searchQuery, (5) atomic searchConcepts, and (6) allowed authorityKinds for legal authority or an empty array for matter documents. Allowed authorityKinds are CASE_LAW, STATUTE, REGULATION, COURT_RULE, AGENCY_GUIDANCE, and ADMINISTRATIVE_DECISION. Include one non-researchable APPLICATION node with applicationDeferred true and requires identifying legal and matter prerequisites. Never use a question as proposition. Never send matter-specific factual conflict to LEGAL_AUTHORITY. Preserve unresolved alternatives and candidate discrimination.',
		OutputSchemaJson = N'{"type":"object","additionalProperties":false,"required":["leaves"],"properties":{"leaves":{"type":"array","minItems":3,"maxItems":12,"items":{"type":"object","additionalProperties":false,"required":["researchKey","researchNeedType","researchQuestion","proposition","sourceClass","researchable","searchQuery","searchConcepts","authorityKinds","applicationDeferred","candidateDiscrimination","parentResearchKey","requires"],"properties":{"researchKey":{"type":"string"},"researchNeedType":{"type":"string","enum":["LEGAL_RULE","LEGAL_AUTHORITY","PROCEDURAL_STANDARD","MATTER_FACT","MATTER_EVIDENCE","APPLICATION","DERIVED"]},"researchQuestion":{"type":"string"},"proposition":{"type":"string"},"sourceClass":{"type":"string","enum":["LEGAL_AUTHORITY","MATTER_DOCUMENT","NONE"]},"researchable":{"type":"boolean"},"searchQuery":{"type":["string","null"]},"searchConcepts":{"type":"array","maxItems":12,"items":{"type":"string"}},"authorityKinds":{"type":"array","maxItems":6,"items":{"type":"string","enum":["CASE_LAW","STATUTE","REGULATION","COURT_RULE","AGENCY_GUIDANCE","ADMINISTRATIVE_DECISION"]}},"applicationDeferred":{"type":"boolean"},"candidateDiscrimination":{"type":"array","minItems":1,"maxItems":10,"items":{"type":"string"}},"parentResearchKey":{"type":["string","null"]},"requires":{"type":"array","maxItems":12,"items":{"type":"string"}}}}}}}',
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE PromptCode = N'DECISION_RESEARCH_NEED'
		AND IsDeleted = 0;
END

COMMIT TRANSACTION;
