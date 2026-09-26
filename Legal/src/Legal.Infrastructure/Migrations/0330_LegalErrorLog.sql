-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0330: Enterprise error log store for the Legal module.
--
-- Purpose: a single DB-backed table that captures detailed error messages AND full stack traces from
-- every module of the pipeline (API request filter, IntelligenceWide2 decision pipeline, LegalRetriever
-- provider routing, OfficialLegalAuthorityRetriever, background workers, etc.). The row is written by a
-- fail-soft IErrorLogService so a logging failure never affects the operation that raised the error.
--
-- Design:
--   * Module            - logical pipeline module that raised the error (e.g. "IntelligenceWide2Service",
--                         "LegalRetriever", "OfficialLegalAuthorityRetriever", "Api").
--   * Operation         - method / action / endpoint within the module.
--   * SeverityCode      - Error / Warning / Critical (free text code, no lookup table required).
--   * Message           - the exception message (or a caller-supplied message).
--   * ExceptionType     - the CLR exception type full name.
--   * StackTrace        - the full stack trace (NVARCHAR(MAX)).
--   * Source            - Exception.Source when available.
--   * CorrelationId     - the pipeline correlation / trace id when known (ties an error to a run).
--   * ContextJson       - optional JSON bag of extra context (tenant scope, request path, ids, ...).
--
-- Base/audit fields are mandatory for every new table in this repository: TenantId, CreatedDateUtc,
-- CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted. TenantId is nullable so errors raised
-- outside an authenticated tenant scope (startup, workers, migrations) can still be recorded.
-- Idempotent: guarded by OBJECT_ID so re-running does nothing once the table exists.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI;');

IF OBJECT_ID(N'POLOXI.Legal_ErrorLog',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_ErrorLog
	(
		ErrorLogId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Legal_ErrorLog_Id DEFAULT NEWID()
						  CONSTRAINT PK_Legal_ErrorLog PRIMARY KEY,
		Module            NVARCHAR(200)    NOT NULL,
		Operation         NVARCHAR(200)    NULL,
		SeverityCode      NVARCHAR(40)     NOT NULL CONSTRAINT DF_Legal_ErrorLog_Severity DEFAULT N'Error',
		Message           NVARCHAR(MAX)    NOT NULL,
		ExceptionType     NVARCHAR(400)    NULL,
		StackTrace        NVARCHAR(MAX)    NULL,
		Source            NVARCHAR(400)    NULL,
		CorrelationId     NVARCHAR(200)    NULL,
		ContextJson       NVARCHAR(MAX)    NULL,
		-- Base / audit fields (mandatory for all new tables in this repository).
		TenantId          UNIQUEIDENTIFIER NULL,
		CreatedDateUtc    DATETIME2(3)     NOT NULL CONSTRAINT DF_Legal_ErrorLog_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId   UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc   DATETIME2(3)     NULL,
		ModifiedByUserId  UNIQUEIDENTIFIER NULL,
		IsDeleted         BIT              NOT NULL CONSTRAINT DF_Legal_ErrorLog_IsDeleted DEFAULT 0
	);

	-- Primary admin query: newest-first within a tenant window (also serves NULL-tenant/global rows).
	CREATE INDEX IX_Legal_ErrorLog_Tenant_Created
		ON POLOXI.Legal_ErrorLog (TenantId, CreatedDateUtc DESC)
		INCLUDE (Module, SeverityCode)
		WHERE IsDeleted = 0;

	-- Module + severity filtering / KPI counts.
	CREATE INDEX IX_Legal_ErrorLog_Module_Severity
		ON POLOXI.Legal_ErrorLog (Module, SeverityCode, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

	-- Correlate all errors raised during a single pipeline run.
	CREATE INDEX IX_Legal_ErrorLog_Correlation
		ON POLOXI.Legal_ErrorLog (CorrelationId)
		WHERE IsDeleted = 0 AND CorrelationId IS NOT NULL;
END

COMMIT TRANSACTION;
