SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- State-specific statute concept rows resolved WITHOUT a jurisdiction anchor, so a California
-- premises-liability matter mentioning "comparative fault" pulled New York CPLR 1411 into every
-- branch's authority list (and the California statute rows did the same on non-California matters).
-- Anchor each state statute to its sovereign so it only resolves when the matter names that state.
-- Empty anchors are preserved for federal/UCC rows (behavior unchanged).
IF OBJECT_ID(N'POLOXI.Legal_LegalConceptAuthority',N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_LegalConceptAuthority
	SET ContextAnchors=N'new york,n.y.,ny,cplr',ModifiedDateUtc=SYSUTCDATETIME()
	WHERE IsDeleted=0 AND (ContextAnchors IS NULL OR ContextAnchors=N'')
	  AND CitationText LIKE N'New York%';

	UPDATE POLOXI.Legal_LegalConceptAuthority
	SET ContextAnchors=N'california,cal.,calif,caci,los angeles,san francisco,san diego,orange county,sacramento,riverside',ModifiedDateUtc=SYSUTCDATETIME()
	WHERE IsDeleted=0 AND (ContextAnchors IS NULL OR ContextAnchors=N'')
	  AND CitationText LIKE N'California%';
END;

COMMIT TRANSACTION;
