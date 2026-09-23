-- Widens intelligence search query storage from NVARCHAR(1000) to NVARCHAR(4000) so long,
-- multi-paragraph analytical prompts (e.g. CTO platform-selection briefs) no longer fail
-- request validation. DTO/request [StringLength] annotations are updated to match.
-- POLOXI.Legal_ExternalKnowledge.NormalizedQuery stays at 400 (index key size limit); it stores
-- short branch-derived seek queries and the repository truncates the cache key defensively.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_Hierarchy',N'NormalizedQuery')=2000
	ALTER TABLE POLOXI.Legal_Hierarchy ALTER COLUMN NormalizedQuery NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'POLOXI.Legal_Execution',N'QueryText')=2000
	ALTER TABLE POLOXI.Legal_Execution ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'POLOXI.Legal_WideExecution',N'QueryText')=2000
	ALTER TABLE POLOXI.Legal_WideExecution ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'AI.Legal_SearchQuery',N'QueryText')=2000
	ALTER TABLE AI.Legal_SearchQuery ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'AI.Legal_SearchQuery',N'NormalizedQuery')=2000
	ALTER TABLE AI.Legal_SearchQuery ALTER COLUMN NormalizedQuery NVARCHAR(4000) NOT NULL;

COMMIT TRANSACTION;
