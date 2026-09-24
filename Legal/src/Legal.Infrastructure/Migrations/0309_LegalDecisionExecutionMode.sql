SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Decision execution modes (DEV Logic / PROD Logic).
--
-- Two execution MODES share the single POLOXI decision pipeline (Core, Legal Domain Pack, Light
-- Evidence Graph, Typed Legal Dependency Graph V2). They are execution settings, not separate
-- reasoning engines. Each mode resolves a default model deployment, whether deterministic AI
-- response replay is permitted, and whether the mode may run in a Production environment.
--
--   DEV  Logic — fast development/debugging; default gpt-4.1-mini; replay allowed; never in Prod.
--   PROD Logic — EXISTING behavior; no forced model (Auto routing when unset); replay disabled; Prod-allowed.
--
-- PROD Logic is the pre-existing execution path: it must resolve exactly as before. It therefore seeds
-- NO DefaultModelCode, so an unset/Auto model selection keeps using the highest-priority active DB
-- route (unchanged behavior). Only DEV Logic pins a lightweight default model. DefaultModelCode, when
-- present, references AI.Legal_ModelDeployment.ModelCode by naming convention; if the named deployment
-- is not configured, the resolver falls back to Auto so seeding a not-yet-deployed code cannot break
-- execution.
--
-- Idempotent: guarded by OBJECT_ID and NOT EXISTS on the deterministic ExecutionModeCode.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionExecutionMode', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionExecutionMode
(
	DecisionExecutionModeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionExecutionMode PRIMARY KEY DEFAULT NEWID(),
	ExecutionModeCode       NVARCHAR(20) NOT NULL,
	DisplayName             NVARCHAR(80) NOT NULL,
	Description             NVARCHAR(400) NULL,
	DefaultModelCode        NVARCHAR(100) NULL,
	AllowReplay             BIT NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_Replay DEFAULT 0,
	IsProductionAllowed     BIT NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_ProdOk DEFAULT 1,
	SortOrder               INT NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_Sort DEFAULT 0,
	IsActive                BIT NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_Active DEFAULT 1,
	TenantId                UNIQUEIDENTIFIER NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_DecisionExecutionMode_IsDeleted DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Legal_DecisionExecutionMode_Code' AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionExecutionMode'))
	CREATE UNIQUE INDEX UX_Legal_DecisionExecutionMode_Code
		ON POLOXI.Legal_DecisionExecutionMode (ExecutionModeCode) WHERE IsDeleted = 0;

-- ── Seed the two canonical modes ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionExecutionMode WHERE ExecutionModeCode = N'DEV')
	INSERT POLOXI.Legal_DecisionExecutionMode
		(ExecutionModeCode, DisplayName, Description, DefaultModelCode, AllowReplay, IsProductionAllowed, SortOrder)
	VALUES
		(N'DEV', N'Dev Logic',
		 N'Fast development and debugging. Runs the full POLOXI decision pipeline with a lightweight default model and optional deterministic AI response replay. Development-only output; blocked in Production.',
		 N'gpt-4.1-mini', 1, 0, 1);

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionExecutionMode WHERE ExecutionModeCode = N'PROD')
	INSERT POLOXI.Legal_DecisionExecutionMode
		(ExecutionModeCode, DisplayName, Description, DefaultModelCode, AllowReplay, IsProductionAllowed, SortOrder)
	VALUES
		(N'PROD', N'Prod Logic',
		 N'Full-quality governed legal decision execution and the pre-existing behavior. Live AI and retrieval providers, Auto model routing when unset, replay disabled, all governance and output-authorization controls preserved.',
		 NULL, 0, 1, 2);

-- ── PROD Logic must equal the pre-existing behavior: never force a model, so unset/Auto selection
--    keeps using the highest-priority active DB route. Normalize any previously seeded model override.
UPDATE POLOXI.Legal_DecisionExecutionMode
	SET DefaultModelCode = NULL
	WHERE ExecutionModeCode = N'PROD' AND DefaultModelCode IS NOT NULL;

COMMIT TRANSACTION;
