-- ============================================================================
-- 0389: Provider portal testing defaults — enable all existing sharing policies.
--
-- WHY: During testing every provider portal must be visible by default. The
-- original schema (0381) defaulted PortalEnabled to 0 (fail-closed), which left
-- the /legal/providercaseview page showing "This provider portal is not
-- enabled. No information is shown." for any policy created before 0381 was
-- changed to default-enabled.
--
-- This migration flips existing (non-deleted) policy rows to PortalEnabled = 1
-- so DB-backed provider data is displayed. It intentionally does NOT widen the
-- privacy flags (ShareOtherProviders / ShareSettlementInfo stay as configured),
-- so enabling the portal never exposes attorney strategy or privileged material.
--
-- Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_ProviderSharingPolicy', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_ProviderSharingPolicy
	SET PortalEnabled    = 1,
		ModifiedDateUtc  = SYSUTCDATETIME()
	WHERE IsDeleted = 0
	  AND PortalEnabled = 0;
END;

COMMIT TRANSACTION;
GO
