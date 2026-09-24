SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Corrective fix for the 0304 Personal Injury sample matter's PracticeAreaCode.
--
-- 0304 originally seeded the seeded matter with the display text PracticeAreaCode = 'Personal Injury'.
-- The /legal/personalinjury workspace filters matters on
--   DecisionDomainPackCodes.PersonalInjury = 'PERSONAL_INJURY'
-- (see LegalPersonalInjury.razor), so the seeded row existed in the DB but was filtered out of the
-- Personal Injury list. Because migrations run once (tracked in dbo._LegalMigrations), the 0304 file
-- cannot self-correct an already-applied environment — this follow-up migration normalizes the code.
--
-- Idempotent: only touches the deterministic seed row when it still holds the old display value.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @User UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @PIM  UNIQUEIDENTIFIER = N'A3000000-0000-0000-0000-000000000001';

IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
	UPDATE POLOXI.Legal_DecisionMatter
	SET PracticeAreaCode = N'PERSONAL_INJURY',
		ModifiedDateUtc  = SYSUTCDATETIME(),
		ModifiedByUserId = @User
	WHERE DecisionMatterId = @PIM
	  AND (PracticeAreaCode IS NULL OR PracticeAreaCode <> N'PERSONAL_INJURY');

COMMIT TRANSACTION;
