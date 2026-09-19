SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — User Management: user login history (Table layer).
--
-- Adds SaaS.Identity_LoginHistory, an append-only record of authentication
-- attempts (both successful and failed sign-ins) used by the tenant-admin
-- Activity surface for audit and security review. The audit-event and usage
-- ledger tables already exist (0243/0244); this migration only introduces the
-- missing login-history table.
--
-- Failed attempts intentionally allow a NULL UserId/TenantId when the email is
-- unknown, so we can still surface brute-force / credential-stuffing patterns.
-- OutcomeCode is DB-backed configuration text (Success | InvalidCredentials |
-- EmailNotVerified | LockedOut). All base/audit fields are present.
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'SaaS.Identity_LoginHistory',N'U') IS NULL
CREATE TABLE SaaS.Identity_LoginHistory
(
	LoginHistoryId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Identity_LoginHistory PRIMARY KEY DEFAULT NEWID(),
	UserId            UNIQUEIDENTIFIER NULL,
	TenantId          UNIQUEIDENTIFIER NULL,
	Email             NVARCHAR(320) NULL,
	OutcomeCode       NVARCHAR(40) NOT NULL,   -- Success | InvalidCredentials | EmailNotVerified | LockedOut
	IsSuccess         BIT NOT NULL CONSTRAINT DF_Identity_LoginHistory_Success DEFAULT 0,
	IpAddress         NVARCHAR(64) NULL,
	UserAgent         NVARCHAR(512) NULL,
	OccurredAtUtc     DATETIME2 NOT NULL CONSTRAINT DF_Identity_LoginHistory_Occurred DEFAULT SYSUTCDATETIME(),
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_Identity_LoginHistory_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_Identity_LoginHistory_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_Identity_LoginHistory_TenantOccurred',N'IX') IS NULL
	CREATE INDEX IX_Identity_LoginHistory_TenantOccurred ON SaaS.Identity_LoginHistory (TenantId, OccurredAtUtc) WHERE IsDeleted = 0;

IF OBJECT_ID(N'IX_Identity_LoginHistory_UserOccurred',N'IX') IS NULL
	CREATE INDEX IX_Identity_LoginHistory_UserOccurred ON SaaS.Identity_LoginHistory (UserId, OccurredAtUtc) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
