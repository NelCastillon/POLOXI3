SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Editable execution SETTINGS per decision mode (DEV Logic / PROD Logic).
--
-- The Configuration Mode admin page lets a Super/Tenant Admin edit the execution settings that drive
-- the decision pipeline for each mode. The mode row already carries DefaultModelCode, AllowReplay and
-- IsProductionAllowed. This migration adds the remaining editable execution overrides so all values
-- are DB-backed (no hardcoded UI/application data):
--
--   ProviderTypeCode   — provider/API family the mode routes chat calls through (Auto when NULL).
--   EndpointReference  — explicit endpoint override (uses the routed deployment endpoint when NULL).
--   ApiVersion         — API version override (uses the routed deployment version when NULL).
--   Temperature        — sampling temperature override (uses the route's temperature when NULL).
--   MaxOutputTokens    — output token ceiling override (uses the route's value when NULL).
--   TimeoutSeconds     — per-call timeout override (uses the route's value when NULL).
--
-- PROD Logic remains behaviorally identical to the pre-existing pipeline while these overrides are
-- NULL: a NULL override means "use the existing Auto/route-resolved value", so blank = unchanged.
--
-- Idempotent: each column guarded by COL_LENGTH.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionExecutionMode', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'ProviderTypeCode') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD ProviderTypeCode NVARCHAR(50) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'EndpointReference') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD EndpointReference NVARCHAR(400) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'ApiVersion') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD ApiVersion NVARCHAR(40) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'Temperature') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD Temperature DECIMAL(3,2) NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'MaxOutputTokens') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD MaxOutputTokens INT NULL;

	IF COL_LENGTH(N'POLOXI.Legal_DecisionExecutionMode', N'TimeoutSeconds') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionExecutionMode ADD TimeoutSeconds INT NULL;
END

COMMIT TRANSACTION;
