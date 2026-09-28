SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- Fail fast instead of blocking indefinitely behind the live app's writes on the
-- usage ledger. Without this the reset can hang waiting on locks.
SET LOCK_TIMEOUT 30000;

-- Yield to the live application rather than blocking its ledger writes.
SET DEADLOCK_PRIORITY LOW;

BEGIN TRANSACTION;

-- Operational correction (one-off usage repair, not a schema change).
--
-- Symptom: Matter Corpus uploads fail with
--   { "meterCode":"legal.decision.run", "limit":100,
--     "message":"Monthly quota exceeded for legal.decision.run (limit 100)." }
-- and "0 uploaded, N failed" in the decision cockpit.
--
-- Root cause: the prepare-only document upload endpoint
-- (POST legal/decision/matters/{matterId}/documents) was gated on the metered
-- 'legal.decision' capability, whose CustomerMeterCode is 'legal.decision.run'
-- (IsMetered = 1). Every prepare-only upload therefore reserved AND committed one
-- decision-run unit against the tenant's 100/month limit. The upload endpoint has
-- now been re-gated on the non-metered 'legal.matters' capability so future uploads
-- no longer consume this meter -- but the units already committed this calendar
-- month remain in SaaS.Commerce_UsageLedger and keep the tenant at the cap until the
-- monthly reset. GetMonthlyUsageAsync sums committed customer ledger rows for the
-- current month (OccurredAtUtc >= first day of month, UsageClass = 'Customer',
-- IsDeleted = 0), so soft-deleting those rows immediately restores capacity.
--
-- Fix (idempotent): soft-delete the current calendar month's committed
-- 'legal.decision.run' customer usage and release any stale (non-committed)
-- reservations for the same meter. Safe to re-run: rows already soft-deleted /
-- released are skipped. Applied to every tenant that accrued usage this month so
-- the reset is not dependent on knowing which tenant hit the cap (dev environment).

IF OBJECT_ID(N'SaaS.Commerce_UsageLedger', N'U') IS NOT NULL
BEGIN
	DECLARE @MeterCode NVARCHAR(100) = N'legal.decision.run';
	DECLARE @MonthStart DATETIME2 = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);

	-- Drive the update per tenant so each statement can seek the leading-TenantId index
	-- (IX_Commerce_UsageLedger_TenantMeter) instead of scanning + table-locking the whole
	-- ledger, which is what caused the migration to hang against the live app's writes.
	-- Snapshot the affected tenant ids into a temp table using a dirty read (NOLOCK) so the
	-- discovery scan never blocks behind the app's in-flight inserts. A dirty read is safe
	-- here: each per-tenant UPDATE below re-checks the exact predicates, so at worst an
	-- uncommitted-then-rolled-back tenant id updates zero rows.
	CREATE TABLE #Tenants (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

	INSERT INTO #Tenants (TenantId)
	SELECT DISTINCT TenantId
	FROM SaaS.Commerce_UsageLedger WITH (NOLOCK)
	WHERE MeterCode = @MeterCode
	  AND UsageClass = N'Customer'
	  AND IsDeleted = 0
	  AND OccurredAtUtc >= @MonthStart;

	DECLARE @TenantId UNIQUEIDENTIFIER;
	DECLARE tenant_cursor CURSOR LOCAL FAST_FORWARD FOR
		SELECT TenantId FROM #Tenants;

	OPEN tenant_cursor;
	FETCH NEXT FROM tenant_cursor INTO @TenantId;
	WHILE @@FETCH_STATUS = 0
	BEGIN
		-- Soft-delete this month's committed customer usage for the meter so
		-- GetMonthlyUsageAsync sees zero consumption for the current billing period.
		UPDATE SaaS.Commerce_UsageLedger
		SET IsDeleted = 1
		WHERE TenantId = @TenantId
		  AND MeterCode = @MeterCode
		  AND UsageClass = N'Customer'
		  AND IsDeleted = 0
		  AND OccurredAtUtc >= @MonthStart;

		-- Release any outstanding reservations for the meter (defensive: reservations are
		-- normally committed immediately, but this clears any that were left holding).
		IF OBJECT_ID(N'SaaS.Commerce_UsageReservation', N'U') IS NOT NULL
		BEGIN
			UPDATE SaaS.Commerce_UsageReservation
			SET StatusCode = N'Released'
			WHERE TenantId = @TenantId
			  AND MeterCode = @MeterCode
			  AND StatusCode = N'Reserved'
			  AND IsDeleted = 0;
		END

		FETCH NEXT FROM tenant_cursor INTO @TenantId;
	END

	CLOSE tenant_cursor;
	DEALLOCATE tenant_cursor;
	DROP TABLE #Tenants;
END

COMMIT TRANSACTION;
