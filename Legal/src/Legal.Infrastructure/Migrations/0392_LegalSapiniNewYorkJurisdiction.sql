-- ============================================================================
-- 0392: Configure the Sapini matter(s) to run as New York (forward-only).
--
-- WHY: Migration 0391 was ALREADY recorded as applied before its Section 3
-- (the Sapini Jurisdiction normalization) was authored, so the migrator skipped
-- re-running it. Investigation then showed TWO distinct problems:
--   * The seeded Sapini matter (A3000003-…000001) still carried the free-text
--     venue string "Supreme Court of the State of New York, Westchester County".
--   * MANY app-created Sapini duplicates (random ids, em-dash title variant) had
--     the INCIDENT LOCATION ("Cedar Street at its intersection with Garden
--     Street, …") stored in the Jurisdiction field instead of a real venue.
-- Neither value matches the canonical seeded JURISDICTION dropdown option, so the
-- jurisdiction detector cannot cleanly resolve New York for these matters.
--
-- This forward-only migration normalizes the Jurisdiction field for EVERY Sapini
-- matter (seed + app duplicates) to the canonical seeded value
-- "New York - Supreme Court, Westchester County" (seeded in 0390 Section 1).
-- Matched on the stable Sapini title stem (hyphen OR em-dash variants) and the
-- motor-vehicle incident signature; idempotent (re-run is a no-op once aligned).
--
-- DATA ONLY. No schema, pipeline, scoring, or UI changes.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
BEGIN
	DECLARE @NyVenue NVARCHAR(200) = N'New York - Supreme Court, Westchester County';

	-- 1) The deterministic seed matter — always normalized.
	UPDATE POLOXI.Legal_DecisionMatter
		SET Jurisdiction = @NyVenue,
			ModifiedDateUtc = SYSUTCDATETIME()
		WHERE DecisionMatterId = N'A3000003-0000-0000-0000-000000000001'
		  AND IsDeleted = 0
		  AND Jurisdiction <> @NyVenue;

	-- 2) Every app-created Sapini duplicate (hyphen OR em-dash title variant).
	--    These stored the incident location in Jurisdiction; align them to the
	--    canonical New York venue so they run as New York like the seed matter.
	UPDATE POLOXI.Legal_DecisionMatter
		SET Jurisdiction = @NyVenue,
			ModifiedDateUtc = SYSUTCDATETIME()
		WHERE IsDeleted = 0
		  AND Title LIKE N'Sapini, Justin%MVA%'
		  AND Jurisdiction <> @NyVenue;
END;

COMMIT TRANSACTION;
