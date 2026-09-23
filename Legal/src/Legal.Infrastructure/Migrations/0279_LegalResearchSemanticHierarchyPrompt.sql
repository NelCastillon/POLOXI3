SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U') IS NOT NULL
	AND COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'ResearchKey') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD ResearchKey NVARCHAR(100) NULL;

IF OBJECT_ID(N'POLOXI.Legal_DecisionPrompt', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt = N'You are performing POLOXI Search-style semantic decomposition for an unresolved decision frontier. Do not answer or resolve the legal question. Produce a small hierarchy of independently resolvable leaves before application. Separate governing legal rule, controlling legal authority, burden or procedural standard, matter fact, matter evidence, and application. Public legal authority can establish only law, standards, and holdings; it cannot establish whether evidence or factual conflict exists in this matter. Matter facts and matter evidence must route only to MATTER_DOCUMENT. APPLICATION and DERIVED nodes must use sourceClass NONE, researchable false, and depend on the leaves needed to resolve them. Do not use directional language such as favor employee protections. Return only strict JSON matching the schema.',
		UserPromptTemplate = N'ORIGINAL QUESTION:\n{{QUERY}}\n\nCURRENT CANDIDATES:\n{{CANDIDATES}}\n\nFRONTIER BRANCH:\n{{FRONTIER}}\n\nCreate at least three leaves and at least two independently researchable leaves. Include one non-researchable APPLICATION node whose requires array identifies the legal and matter leaves needed for resolution. For a factual-dispute frontier, generate precise legal-rule or procedural-standard questions for public authority plus separate matter-fact and matter-evidence questions for matter documents. Preserve unresolved alternatives and candidate discrimination. Never send a proposition asking whether conflicting evidence exists in this matter to LEGAL_AUTHORITY.',
		OutputSchemaJson = N'{"type":"object","additionalProperties":false,"required":["leaves"],"properties":{"leaves":{"type":"array","minItems":3,"maxItems":12,"items":{"type":"object","additionalProperties":false,"required":["researchKey","researchNeedType","proposition","sourceClass","researchable","candidateDiscrimination","parentResearchKey","requires"],"properties":{"researchKey":{"type":"string"},"researchNeedType":{"type":"string","enum":["LEGAL_RULE","LEGAL_AUTHORITY","PROCEDURAL_STANDARD","MATTER_FACT","MATTER_EVIDENCE","APPLICATION","DERIVED"]},"proposition":{"type":"string"},"sourceClass":{"type":"string","enum":["LEGAL_AUTHORITY","MATTER_DOCUMENT","NONE"]},"researchable":{"type":"boolean"},"candidateDiscrimination":{"type":"array","minItems":1,"maxItems":10,"items":{"type":"string"}},"parentResearchKey":{"type":["string","null"]},"requires":{"type":"array","maxItems":12,"items":{"type":"string"}}}}}}}',
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE PromptCode = N'DECISION_RESEARCH_NEED'
		AND IsDeleted = 0;
END

COMMIT TRANSACTION;
