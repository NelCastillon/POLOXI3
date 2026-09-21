SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'SourceClassCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD SourceClassCode NVARCHAR(40) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'IsResearchable') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD IsResearchable BIT NOT NULL
			CONSTRAINT DF_Legal_DecisionResearchNeed_IsResearchable DEFAULT 1;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'ParentResearchKey') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD ParentResearchKey NVARCHAR(100) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'ResearchKey') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD ResearchKey NVARCHAR(100) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'RequiredResearchKeysJson') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD RequiredResearchKeysJson NVARCHAR(MAX) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'CandidateDiscriminationJson') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD CandidateDiscriminationJson NVARCHAR(MAX) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'SemanticProposalStatusCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD SemanticProposalStatusCode NVARCHAR(40) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'SemanticProposalReasonCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD SemanticProposalReasonCode NVARCHAR(100) NULL;
END

IF OBJECT_ID(N'POLOXI.Legal_DecisionPrompt', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionPrompt AS target
	USING (VALUES
		(N'DECISION_RESEARCH_NEED', N'RESEARCH_NEED',
			 N'You are performing POLOXI Search-style semantic decomposition for an unresolved decision frontier. Do not answer or resolve the legal question. Produce a small hierarchy of independently resolvable leaves before application. Separate governing legal rule, controlling legal authority, burden or procedural standard, matter fact, matter evidence, and application. Public legal authority can establish only law, standards, and holdings; it cannot establish whether evidence or factual conflict exists in this matter. Matter facts and matter evidence must route only to MATTER_DOCUMENT. APPLICATION and DERIVED nodes must use sourceClass NONE, researchable false, and depend on the leaves needed to resolve them. Do not use directional language such as favor employee protections. Return only strict JSON matching the schema.',
			 N'ORIGINAL QUESTION:\n{{QUERY}}\n\nCURRENT CANDIDATES:\n{{CANDIDATES}}\n\nFRONTIER BRANCH:\n{{FRONTIER}}\n\nCreate at least three leaves and at least two independently researchable leaves. Include one non-researchable APPLICATION node whose requires array identifies the legal and matter leaves needed for resolution. For a factual-dispute frontier, generate precise legal-rule or procedural-standard questions for public authority plus separate matter-fact and matter-evidence questions for matter documents. Preserve unresolved alternatives and candidate discrimination. Never send a proposition asking whether conflicting evidence exists in this matter to LEGAL_AUTHORITY.',
			 N'{"type":"object","additionalProperties":false,"required":["leaves"],"properties":{"leaves":{"type":"array","minItems":3,"maxItems":12,"items":{"type":"object","additionalProperties":false,"required":["researchKey","researchNeedType","proposition","sourceClass","researchable","candidateDiscrimination","parentResearchKey","requires"],"properties":{"researchKey":{"type":"string"},"researchNeedType":{"type":"string","enum":["LEGAL_RULE","LEGAL_AUTHORITY","PROCEDURAL_STANDARD","MATTER_FACT","MATTER_EVIDENCE","APPLICATION","DERIVED"]},"proposition":{"type":"string"},"sourceClass":{"type":"string","enum":["LEGAL_AUTHORITY","MATTER_DOCUMENT","NONE"]},"researchable":{"type":"boolean"},"candidateDiscrimination":{"type":"array","minItems":1,"maxItems":10,"items":{"type":"string"}},"parentResearchKey":{"type":["string","null"]},"requires":{"type":"array","maxItems":12,"items":{"type":"string"}}}}}}}')
	) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	ON target.PromptCode = source.PromptCode
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
		VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);
END

COMMIT TRANSACTION;
