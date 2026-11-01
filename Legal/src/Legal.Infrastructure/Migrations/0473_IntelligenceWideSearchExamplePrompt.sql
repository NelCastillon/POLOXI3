SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- Database-backed example prompt chips for the /legal/search composer. These power the
-- suggestion chips shown under the search box before a search runs. No hardcoded UI content:
-- the UI loads these rows through the Table -> repository -> service -> API -> ApiClient chain,
-- exactly like POLOXI.Legal_SearchContext. Platform (tenant-null) rows are global defaults and
-- are overridable per tenant.
IF OBJECT_ID(N'POLOXI.Legal_SearchExamplePrompt',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_SearchExamplePrompt
	(
		SearchExamplePromptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SearchExamplePrompt PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		Label NVARCHAR(120) NOT NULL,
		PromptText NVARCHAR(1000) NOT NULL,
		ContextCode NVARCHAR(50) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_SearchExamplePrompt_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_SearchExamplePrompt_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_SearchExamplePrompt_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_SearchExamplePrompt_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_SearchExamplePrompt_TenantLabel ON POLOXI.Legal_SearchExamplePrompt(TenantId,Label) WHERE IsDeleted=0;
END;

-- Seed platform (tenant-null) example prompts. Idempotent via MERGE on (TenantId IS NULL, Label).
MERGE POLOXI.Legal_SearchExamplePrompt AS target
USING (VALUES
	(N'Statute of limitations', N'What is the statute of limitations for a personal injury claim and when does the clock start running?', 10),
	(N'Compare precedents', N'Summarize and compare the leading precedents that control liability for a slip-and-fall on commercial property.', 20),
	(N'Draft a demand letter', N'Draft a demand letter outline for a motor-vehicle negligence claim, including damages categories to assert.', 30),
	(N'Regulatory requirements', N'What federal regulations govern mandatory disclosures for this matter, and where are they codified?', 40),
	(N'Assess the evidence', N'Identify the strongest and weakest evidence for proving negligence in a premises-liability case.', 50)
) AS source(Label,PromptText,SortOrder)
   ON target.TenantId IS NULL AND target.Label=source.Label AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET
		target.PromptText=source.PromptText,
		target.SortOrder=source.SortOrder,
		target.IsActive=1,
		target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(SearchExamplePromptId,TenantId,Label,PromptText,ContextCode,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.Label,source.PromptText,NULL,1,source.SortOrder,SYSUTCDATETIME(),0);

COMMIT TRANSACTION;
