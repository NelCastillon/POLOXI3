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
		 N'You are performing POLOXI semantic decomposition. Your task is not to answer the legal question. Identify the smallest independently researchable propositions necessary to resolve the specified unresolved decision branch. Preserve the original question. Do not assume missing facts or convert an unresolved question into an asserted conclusion. Separate governing legal rule, legal authority, burden or procedural standard, matter fact, matter evidence, and application. Each leaf must be capable of being supported, contradicted, or left unresolved by its assigned source class. APPLICATION and DERIVED nodes are not retrievable. Return only strict JSON matching the schema.',
		 N'ORIGINAL QUESTION:\n{{QUERY}}\n\nCURRENT CANDIDATES:\n{{CANDIDATES}}\n\nFRONTIER BRANCH:\n{{FRONTIER}}\n\nDecompose only as deeply as needed. Preserve unresolved alternatives. Every leaf must identify its source class and the candidate codes it discriminates. Do not combine law, fact, evidence, and application in one proposition.',
		 N'{"type":"object","additionalProperties":false,"required":["leaves"],"properties":{"leaves":{"type":"array","minItems":1,"maxItems":12,"items":{"type":"object","additionalProperties":false,"required":["researchKey","researchNeedType","proposition","sourceClass","researchable","candidateDiscrimination","parentResearchKey","requires"],"properties":{"researchKey":{"type":"string"},"researchNeedType":{"type":"string","enum":["LEGAL_RULE","LEGAL_AUTHORITY","PROCEDURAL_STANDARD","MATTER_FACT","MATTER_EVIDENCE","APPLICATION","DERIVED"]},"proposition":{"type":"string"},"sourceClass":{"type":"string","enum":["LEGAL_AUTHORITY","MATTER_DOCUMENT","NONE"]},"researchable":{"type":"boolean"},"candidateDiscrimination":{"type":"array","maxItems":10,"items":{"type":"string"}},"parentResearchKey":{"type":["string","null"]},"requires":{"type":"array","maxItems":12,"items":{"type":"string"}}}}}}}')
	) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	ON target.PromptCode = source.PromptCode
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
		VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);
END

COMMIT TRANSACTION;
