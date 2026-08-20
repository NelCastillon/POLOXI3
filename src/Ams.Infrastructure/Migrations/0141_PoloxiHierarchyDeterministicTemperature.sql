SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- The EPH hierarchy proposal must be as deterministic as possible so the same
-- question yields the same approved branch set and stable result counts.
IF OBJECT_ID(N'AI.FeaturePolicy',N'U') IS NOT NULL
BEGIN
	UPDATE AI.FeaturePolicy
	SET Temperature=CONVERT(decimal(4,3),0.000),
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE FeatureCode=N'INTELLIGENCE_EPH_HIERARCHY' AND IsDeleted=0 AND Temperature<>CONVERT(decimal(4,3),0.000);
END;

COMMIT TRANSACTION;
