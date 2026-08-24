-- Widens intelligence search query storage from NVARCHAR(1000) to NVARCHAR(4000) so long,
-- multi-paragraph analytical prompts (e.g. CTO platform-selection briefs) no longer fail
-- request validation. DTO/request [StringLength] annotations are updated to match.
-- POLOXI.ExternalKnowledge.NormalizedQuery stays at 400 (index key size limit); it stores
-- short branch-derived seek queries and the repository truncates the cache key defensively.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Hierarchy',N'NormalizedQuery')=2000
	ALTER TABLE POLOXI.Hierarchy ALTER COLUMN NormalizedQuery NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'POLOXI.Execution',N'QueryText')=2000
	ALTER TABLE POLOXI.Execution ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'POLOXI.WideExecution',N'QueryText')=2000
	ALTER TABLE POLOXI.WideExecution ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'AI.SearchQuery',N'QueryText')=2000
	ALTER TABLE AI.SearchQuery ALTER COLUMN QueryText NVARCHAR(4000) NOT NULL;

IF COL_LENGTH(N'AI.SearchQuery',N'NormalizedQuery')=2000
	ALTER TABLE AI.SearchQuery ALTER COLUMN NormalizedQuery NVARCHAR(4000) NOT NULL;

COMMIT TRANSACTION;
