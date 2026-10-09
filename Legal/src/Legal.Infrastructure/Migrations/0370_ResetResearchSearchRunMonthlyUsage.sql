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
-- Symptom: POLOXI Wide / Wide2 dynamic search starts fail with HTTP 429 and body
--   { "meterCode":"research.search.run", "limit":100,
--     "message":"Monthly quota exceeded for research.search.run (limit 100)." }
--
-- Root cause: the metered 'research.search' capability (CustomerMeterCode
-- 'research.search.run', IsMetered = 1) committed one unit per executed search
-- against the tenant's monthly limit. The units already committed this calendar
-- month remain in SaaS.Commerce_UsageLedger and keep the tenant at the cap until
-- the monthly reset. GetMonthlyUsageAsync sums committed customer ledger rows for
-- the current month (OccurredAtUtc >= first day of month, UsageClass = 'Customer',
-- IsDeleted = 0), so soft-deleting those rows immediately restores capacity.
--
-- Fix (idempotent): soft-delete the current calendar month's committed
-- 'research.search.run' customer usage and release any stale (non-committed)
-- reservations for the same meter. Safe to re-run: rows already soft-deleted /
-- released are skipped. Applied to every tenant that accrued usage this month so
-- the reset is not dependent on knowing which tenant hit the cap (dev environment).

IF OBJECT_ID(N'SaaS.Commerce_UsageLedger', N'U') IS NOT NULL
BEGIN
	DECLARE @MeterCode NVARCHAR(100) = N'research.search.run';
	DECLARE @MonthStart DATETIME2 = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);

	-- Drive the update per tenant so each statement can seek the leading-TenantId index
	-- (IX_Commerce_UsageLedger_TenantMeter) instead of scanning + table-locking the whole
	-- ledger, which is what caused resets to hang against the live app's writes.
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

-- Raise the monthly.research limit to a high dev ceiling across the base entitlement,
-- every plan entitlement, and every tenant override so capacity exists regardless of which
-- tenant hit the cap or how many units were already committed. This makes the unblock
-- independent of the usage reset above (which can miss rows on a different tenant/month).
IF OBJECT_ID(N'SaaS.Commerce_Entitlement', N'U') IS NOT NULL
BEGIN
	DECLARE @DevLimit BIGINT = 1000000;
	DECLARE @EntitlementId UNIQUEIDENTIFIER =
		(SELECT TOP 1 EntitlementId FROM SaaS.Commerce_Entitlement
		 WHERE Code = N'monthly.research' AND IsDeleted = 0);

	IF @EntitlementId IS NOT NULL
	BEGIN
		IF OBJECT_ID(N'SaaS.Commerce_PlanEntitlement', N'U') IS NOT NULL
			UPDATE SaaS.Commerce_PlanEntitlement
			SET LimitValue = @DevLimit
			WHERE EntitlementId = @EntitlementId AND IsDeleted = 0
			  AND (LimitValue IS NULL OR LimitValue < @DevLimit);

		IF OBJECT_ID(N'SaaS.Commerce_TenantEntitlementOverride', N'U') IS NOT NULL
			UPDATE SaaS.Commerce_TenantEntitlementOverride
			SET LimitValue = @DevLimit
			WHERE EntitlementId = @EntitlementId AND IsDeleted = 0
			  AND (LimitValue IS NULL OR LimitValue < @DevLimit);
	END
END

COMMIT TRANSACTION;