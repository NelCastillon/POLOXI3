SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — Identity support tables (Table layer, phase 1).
--
-- ASP.NET Core Identity owns AspNetUsers/Roles/Claims/Tokens (added later via EF
-- Core). This migration only adds the Judz-specific support tables that Identity
-- does not provide out of the box:
--   * Identity_EmailVerificationChallenge — hashed, single-use, time-limited
--     6-digit signup verification codes with attempt/resend throttling.
--   * Identity_ProvisioningState — idempotent workspace-provisioning tracking so
--     a double-submitted verification never provisions two workspaces.
--
-- Verification codes are NEVER stored in plaintext (CodeHash only). All tables
-- carry the standard base/audit fields. Selectable/config values are DB-backed.
-- ─────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'SaaS') IS NULL
	EXEC(N'CREATE SCHEMA SaaS AUTHORIZATION dbo;');

-- Email verification challenge ────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.Identity_EmailVerificationChallenge',N'U') IS NULL
CREATE TABLE SaaS.Identity_EmailVerificationChallenge
(
	ChallengeId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Identity_EmailVerificationChallenge PRIMARY KEY DEFAULT NEWID(),
	UserId            UNIQUEIDENTIFIER NOT NULL,
	Purpose           NVARCHAR(60) NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_Purpose DEFAULT N'EmailVerification',
	CodeHash          VARBINARY(64) NOT NULL,
	ExpiresAtUtc      DATETIME2 NOT NULL,
	AttemptCount      INT NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_Attempts DEFAULT 0,
	MaxAttempts       INT NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_MaxAttempts DEFAULT 5,
	ResendCount       INT NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_ResendCount DEFAULT 0,
	ConsumedAtUtc     DATETIME2 NULL,
	LastSentAtUtc     DATETIME2 NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Identity_EmailVerificationChallenge_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Identity_EmailVerificationChallenge_UserPurpose',N'IX') IS NULL
	CREATE INDEX IX_Identity_EmailVerificationChallenge_UserPurpose
		ON SaaS.Identity_EmailVerificationChallenge (UserId, Purpose)
		WHERE IsDeleted = 0 AND ConsumedAtUtc IS NULL;

-- Workspace provisioning state (idempotency guard) ─────────────────────────────
IF OBJECT_ID(N'SaaS.Identity_ProvisioningState',N'U') IS NULL
CREATE TABLE SaaS.Identity_ProvisioningState
(
	ProvisioningStateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Identity_ProvisioningState PRIMARY KEY DEFAULT NEWID(),
	UserId              UNIQUEIDENTIFIER NOT NULL,
	StatusCode          NVARCHAR(40) NOT NULL CONSTRAINT DF_Identity_ProvisioningState_Status DEFAULT N'NotStarted',
	TenantId            UNIQUEIDENTIFIER NULL,
	FailureReason       NVARCHAR(MAX) NULL,
	StartedAtUtc        DATETIME2 NULL,
	CompletedAtUtc      DATETIME2 NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Identity_ProvisioningState_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Identity_ProvisioningState_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'UX_Identity_ProvisioningState_UserId',N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_Identity_ProvisioningState_UserId
		ON SaaS.Identity_ProvisioningState (UserId)
		WHERE IsDeleted = 0;

COMMIT TRANSACTION;
