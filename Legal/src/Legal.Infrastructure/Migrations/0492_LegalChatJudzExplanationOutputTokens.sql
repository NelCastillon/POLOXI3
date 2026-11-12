SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Chat Judz now grounds citation prompts on the matter's best Deep Citation run (25+ linked
-- authorities + branches rendered as side-by-side passage tables). The INTELLIGENCE_POLOXI_EXPLANATION
-- feature policy was originally seeded (0139/0180) with MaximumOutputTokens=2000, which is far too
-- small for these richer answers: AzureOpenAiProvider throws InvalidOperationException ("output was
-- truncated because the completion hit the configured maximum output tokens (2000)") and the router
-- exhausts every route (AiProviderUnavailableException). Raise the output budget to 16000 for every
-- tenant's explanation policy so premium citation-linked answers complete without truncation.
IF OBJECT_ID(N'AI.Legal_FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.Legal_FeaturePolicy
	SET MaximumOutputTokens = 16000,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE FeatureCode = N'INTELLIGENCE_POLOXI_EXPLANATION'
	  AND IsDeleted = 0
	  AND MaximumOutputTokens < 16000;
END;

COMMIT TRANSACTION;
