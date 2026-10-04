-- ============================================================================
-- 0381: Provider portal persistence — attorney-approved sharing policy and
--       firm→provider requests.
--
-- WHY: The provider portal (/legal/providercaseview) must show ONLY what the
-- attorney has explicitly approved for a specific treating provider, plus the
-- items the firm needs from that provider. Both are durable, tenant-scoped,
-- matter + provider keyed records — never hardcoded or inferred.
--
--  POLOXI.Legal_ProviderSharingPolicy
--    One row per (matter, provider). Boolean share flags drive the provider
--    projection. Absence of a row means "nothing shared" (fail-closed).
--
--  POLOXI.Legal_ProviderRequest
--    Firm-to-provider asks ("What the firm needs from you") with a lifecycle
--    status. Never deleted; status transitions preserve history.
--
-- All tables carry the standard base/audit fields. Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI;');

-- ── Sharing policy ───────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_ProviderSharingPolicy', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_ProviderSharingPolicy
	(
		ProviderSharingPolicyId UNIQUEIDENTIFIER NOT NULL
			CONSTRAINT PK_Legal_ProviderSharingPolicy PRIMARY KEY
			CONSTRAINT DF_Legal_ProviderSharingPolicy_Id DEFAULT NEWID(),
		TenantId                UNIQUEIDENTIFIER NOT NULL,
		MatterId                UNIQUEIDENTIFIER NOT NULL,
		ProviderKey             NVARCHAR(256)    NOT NULL,   -- normalized provider name
		ProviderDisplayName     NVARCHAR(256)    NULL,
		PortalEnabled           BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Portal DEFAULT 1,
		ShareMatterStatus       BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Status DEFAULT 1,
		ShareCurrentStage       BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Stage DEFAULT 1,
		SharePatientTreatment   BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Treat DEFAULT 1,
		ShareOwnRecords         BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Records DEFAULT 1,
		ShareOwnBills           BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Bills DEFAULT 1,
		ShareFirmRequests       BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Requests DEFAULT 1,
		ShareOtherProviders     BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Others DEFAULT 0,
		ShareSettlementInfo     BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Settlement DEFAULT 0,
		CreatedDateUtc          DATETIME2        NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId         UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc         DATETIME2        NULL,
		ModifiedByUserId        UNIQUEIDENTIFIER NULL,
		IsDeleted               BIT              NOT NULL CONSTRAINT DF_Legal_ProviderSharingPolicy_Deleted DEFAULT 0
	);

	CREATE UNIQUE INDEX UX_Legal_ProviderSharingPolicy_Matter_Provider
		ON POLOXI.Legal_ProviderSharingPolicy (TenantId, MatterId, ProviderKey)
		WHERE IsDeleted = 0;
END;

-- ── Firm → provider requests ─────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_ProviderRequest', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_ProviderRequest
	(
		ProviderRequestId   UNIQUEIDENTIFIER NOT NULL
			CONSTRAINT PK_Legal_ProviderRequest PRIMARY KEY
			CONSTRAINT DF_Legal_ProviderRequest_Id DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		MatterId            UNIQUEIDENTIFIER NOT NULL,
		ProviderKey         NVARCHAR(256)    NOT NULL,
		Title               NVARCHAR(256)    NOT NULL,
		Detail              NVARCHAR(1024)   NULL,
		RequestKind         NVARCHAR(64)     NOT NULL CONSTRAINT DF_Legal_ProviderRequest_Kind DEFAULT N'DOCUMENT',
		StatusCode          NVARCHAR(32)     NOT NULL CONSTRAINT DF_Legal_ProviderRequest_Status DEFAULT N'OPEN',
		RequestedDateUtc    DATETIME2        NOT NULL CONSTRAINT DF_Legal_ProviderRequest_Requested DEFAULT SYSUTCDATETIME(),
		FulfilledDateUtc    DATETIME2        NULL,
		CreatedDateUtc      DATETIME2        NOT NULL CONSTRAINT DF_Legal_ProviderRequest_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL CONSTRAINT DF_Legal_ProviderRequest_Deleted DEFAULT 0
	);

	CREATE INDEX IX_Legal_ProviderRequest_Matter_Provider
		ON POLOXI.Legal_ProviderRequest (TenantId, MatterId, ProviderKey)
		WHERE IsDeleted = 0;
END;

COMMIT TRANSACTION;
GO
