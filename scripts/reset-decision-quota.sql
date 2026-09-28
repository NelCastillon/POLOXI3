-- ============================================================================
-- reset-decision-quota.sql  (DEV ONLY -- run manually)
--
-- Symptom: Matter Corpus uploads fail with
--   "0 uploaded, N failed: ... {"meterCode":"legal.decision.run","limit":100,
--    "message":"Monthly quota exceeded for legal.decision.run (limit 100)."}"
--
-- Cause: Every Decision Intelligence run (including each upload that triggers the
-- Matter Change Processor) records one unit against the customer meter
-- 'legal.decision.run'. The monthly allowance comes from the 'monthly.legal_decision'
-- entitlement on the tenant's plan. Repeated local testing exhausts it.
--
-- SaasRepository.GetMonthlyUsageAsync counts SaaS.Commerce_UsageLedger rows where
-- UsageClass='Customer', IsDeleted=0, and OccurredAtUtc >= first-of-current-month.
--
-- This script (idempotent, DEV only):
--   STEP 1  Soft-deletes the CURRENT MONTH 'legal.decision.run' customer ledger rows
--           so monthly usage resets to 0 immediately.
--   STEP 2  (Optional) Raises the 'monthly.legal_decision' plan limit for headroom
--           during extended local testing. Adjust @NewMonthlyLimit or comment out.
--
-- Run against the same dev database configured in appsettings.Development.json,
-- then re-run the upload. No API restart required.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @MeterCode        NVARCHAR(100) = N'legal.decision.run';
DECLARE @EntitlementCode  NVARCHAR(100) = N'monthly.legal_decision';
DECLARE @NewMonthlyLimit  BIGINT        = 100000;  -- <-- generous DEV headroom

-- STEP 1: reset current-month usage for the decision meter.
IF OBJECT_ID(N'SaaS.Commerce_UsageLedger', N'U') IS NOT NULL
BEGIN
	UPDATE SaaS.Commerce_UsageLedger
		SET IsDeleted = 1
	WHERE MeterCode = @MeterCode
	  AND UsageClass = N'Customer'
	  AND IsDeleted = 0
	  AND OccurredAtUtc >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);

	PRINT CONCAT(N'Reset ', @@ROWCOUNT, N' current-month usage rows for ', @MeterCode, N'.');
END

-- STEP 2 (optional): raise the plan limit for the decision entitlement so extended
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
END

COMMIT TRANSACTION;
