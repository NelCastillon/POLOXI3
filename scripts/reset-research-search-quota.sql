-- ============================================================================
-- reset-research-search-quota.sql  (DEV ONLY -- run manually, re-runnable)
--
-- Symptom: Wide research search fails with HTTP 429 after retries, e.g.
--   The API throttled POST api/intelligence_wide/search/dynamic/start after 4
--   attempts: {"meterCode":"research.search.run","limit":100,
--    "message":"Monthly quota exceeded for research.search.run (limit 100)."}
--
-- Cause: The 'research.search' capability (IsMetered = 1, CustomerMeterCode =
-- 'research.search.run') records one unit per executed search against the
-- customer meter. The monthly allowance comes from the 'monthly.research'
-- entitlement on the tenant's plan (seeded at a small launch limit in migration
-- 0245). Repeated local testing exhausts it. This is NOT a code defect --
-- CapabilityGate correctly returns 429 once the quota is consumed.
--
-- Migration 0370_ResetResearchSearchRunMonthlyUsage.sql fixed this once, but
-- migrations run only ONCE; usage re-accumulates every month. This standalone
-- script is idempotent and safe to re-run anytime against the dev database
-- configured in appsettings.Development.json. No API restart required.
--
-- SaasRepository.GetMonthlyUsageAsync counts SaaS.Commerce_UsageLedger rows where
-- UsageClass='Customer', IsDeleted=0, and OccurredAtUtc >= first-of-current-month.
--
--   STEP 1  Soft-deletes the CURRENT MONTH 'research.search.run' customer ledger
--           rows (per tenant) so monthly usage resets to 0 immediately, and
--           releases any outstanding reservations for the meter.
--   STEP 2  (Optional) Raises the 'monthly.research' plan limit for DEV headroom.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @MeterCode        NVARCHAR(100) = N'research.search.run';
DECLARE @EntitlementCode  NVARCHAR(100) = N'monthly.research';
DECLARE @NewMonthlyLimit  BIGINT        = 1000000;  -- <-- generous DEV headroom
DECLARE @MonthStart       DATETIME2     = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);

-- STEP 1: reset current-month usage for the research search meter.
IF OBJECT_ID(N'SaaS.Commerce_UsageLedger', N'U') IS NOT NULL
BEGIN
	UPDATE SaaS.Commerce_UsageLedger
		SET IsDeleted = 1
	WHERE MeterCode = @MeterCode
	  AND UsageClass = N'Customer'
	  AND IsDeleted = 0
	  AND OccurredAtUtc >= @MonthStart;

	PRINT CONCAT(N'Reset ', @@ROWCOUNT, N' current-month usage rows for ', @MeterCode, N'.');

	-- Release any outstanding reservations for the meter (defensive: reservations
	-- are normally committed immediately, but this clears any left holding).
	IF OBJECT_ID(N'SaaS.Commerce_UsageReservation', N'U') IS NOT NULL
	BEGIN
		UPDATE SaaS.Commerce_UsageReservation
			SET StatusCode = N'Released'
		WHERE MeterCode = @MeterCode
		  AND StatusCode = N'Reserved'
		  AND IsDeleted = 0;

		PRINT CONCAT(N'Released ', @@ROWCOUNT, N' outstanding reservation(s) for ', @MeterCode, N'.');
	END
END

-- STEP 2 (optional): raise the plan limit for the research entitlement so extended
-- local testing does not re-exhaust the monthly quota. Comment out to keep launch quota.
IF OBJECT_ID(N'SaaS.Commerce_PlanEntitlement', N'U') IS NOT NULL
   AND OBJECT_ID(N'SaaS.Commerce_Entitlement', N'U') IS NOT NULL
BEGIN
	UPDATE pe
		SET pe.LimitValue = @NewMonthlyLimit
	FROM SaaS.Commerce_PlanEntitlement pe
	JOIN SaaS.Commerce_Entitlement e
		ON e.EntitlementId = pe.EntitlementId
	   AND e.Code = @EntitlementCode
	   AND e.IsDeleted = 0
	WHERE pe.IsDeleted = 0
	  AND (pe.LimitValue IS NULL OR pe.LimitValue < @NewMonthlyLimit);

	PRINT CONCAT(N'Raised ', @@ROWCOUNT, N' plan entitlement limit row(s) for ', @EntitlementCode, N' to ', @NewMonthlyLimit, N'.');

	-- Also lift any per-tenant override so capacity exists regardless of which tenant hit the cap.
	IF OBJECT_ID(N'SaaS.Commerce_TenantEntitlementOverride', N'U') IS NOT NULL
	BEGIN
		UPDATE teo
			SET teo.LimitValue = @NewMonthlyLimit
		FROM SaaS.Commerce_TenantEntitlementOverride teo
		JOIN SaaS.Commerce_Entitlement e
			ON e.EntitlementId = teo.EntitlementId
		   AND e.Code = @EntitlementCode
		   AND e.IsDeleted = 0
		WHERE teo.IsDeleted = 0
		  AND (teo.LimitValue IS NULL OR teo.LimitValue < @NewMonthlyLimit);

		PRINT CONCAT(N'Raised ', @@ROWCOUNT, N' tenant override limit row(s) for ', @EntitlementCode, N' to ', @NewMonthlyLimit, N'.');
	END
END