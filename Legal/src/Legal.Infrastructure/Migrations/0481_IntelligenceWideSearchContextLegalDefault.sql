SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- /legal/search Context dropdown: LEGAL becomes the platform default and both rows carry
-- actionable "when to use" guidance (rendered as the option tooltip). Reasoning engine is the
-- same for both; they differ only in grounding source and evidence admission strictness.
IF OBJECT_ID(N'POLOXI.Legal_SearchContext',N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_SearchContext AS target
	USING (VALUES
		(N'LEGAL',N'Legal',N'Recommended for anything you will cite. Grounds on CourtListener case law and GovInfo/eCFR statutes and regulations; only identity-verified primary authorities count as evidence.',1,1,1),
		(N'GENERAL',N'General',N'Use for breadth, procedure, current events, or non-authority questions. Grounds on live web search (Tavily); web pages count as evidence without authority verification.',0,1,2)
	) AS source(ContextCode,DisplayName,Description,IsDefault,IsActive,SortOrder)
	   ON target.TenantId IS NULL AND target.ContextCode=source.ContextCode AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.DisplayName=source.DisplayName,
			target.Description=source.Description,
			target.IsDefault=source.IsDefault,
			target.IsActive=source.IsActive,
			target.SortOrder=source.SortOrder,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT(SearchContextId,TenantId,ContextCode,DisplayName,Description,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,source.ContextCode,source.DisplayName,source.Description,source.IsDefault,source.IsActive,source.SortOrder,SYSUTCDATETIME(),0);
END;

-- Retire the orphaned "_Astra" external-grounding setting copies: no code path reads them and the
-- stale placeholder they masked caused Tavily 401s. Soft-delete so history is preserved.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHERE IsDeleted=0 AND SettingKey LIKE N'Intelligence.SearchWide.ExternalGrounding.%[_]Astra';
END;

COMMIT TRANSACTION;
