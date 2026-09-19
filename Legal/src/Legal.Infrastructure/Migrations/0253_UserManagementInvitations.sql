SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────
-- Judz.ai Early Access SaaS — User Management Phase B: tenant invitations,
-- pending role assignments, and a transactional email outbox (Table layer).
--
-- An invitation is a tenant-scoped, single-use, time-limited grant that lets a
-- global identity join a tenant with a pre-selected role and (later) group set.
-- Tokens are NEVER stored in plaintext — only a SHA-256 hash (TokenHash) is
-- persisted, mirroring Identity_EmailVerificationChallenge.CodeHash.
--
--   • SaaS_TenantInvitation      — the invitation lifecycle record.
--   • SaaS_TenantInvitationRole  — pending role assignments applied on acceptance
--                                  (Groups are deferred to Phase C).
--   • SaaS_Outbox                — retry-safe transactional email queue drained by
--                                  a background worker that reuses IJudzEmailSender.
--
-- StatusCode semantics (DB-authoritative, validated in the service):
--   Invitation : Pending, Accepted, Expired, Revoked
--   Outbox     : Pending, Sent, Failed
-- All tables carry the standard base/audit fields. Idempotent / safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. SaaS_TenantInvitation ────────────────────────────────────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantInvitation', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantInvitation
(
	InvitationId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantInvitation PRIMARY KEY DEFAULT NEWID(),
	TenantId          UNIQUEIDENTIFIER NOT NULL,
	EmailNormalized   NVARCHAR(256) NOT NULL,
	RoleId            UNIQUEIDENTIFIER NOT NULL,
	TokenHash         VARBINARY(64) NOT NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_TenantInvitation_Status DEFAULT N'Pending',
	InvitedByUserId   UNIQUEIDENTIFIER NULL,
	ExpiresAtUtc      DATETIME2 NOT NULL,
	AcceptedAtUtc     DATETIME2 NULL,
	AcceptedByUserId  UNIQUEIDENTIFIER NULL,
	RevokedAtUtc      DATETIME2 NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantInvitation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantInvitation_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantInvitation_Tenant FOREIGN KEY (TenantId) REFERENCES SaaS.SaaS_Tenant (TenantId),
	CONSTRAINT FK_SaaS_TenantInvitation_Role FOREIGN KEY (RoleId) REFERENCES SaaS.SaaS_Role (RoleId)
);

-- Only one active (Pending, not soft-deleted) invitation per tenant + email.
IF OBJECT_ID(N'UX_SaaS_TenantInvitation_ActiveEmail', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantInvitation_ActiveEmail
		ON SaaS.SaaS_TenantInvitation (TenantId, EmailNormalized)
		WHERE IsDeleted = 0 AND StatusCode = N'Pending';
IF OBJECT_ID(N'IX_SaaS_TenantInvitation_Tenant', N'IX') IS NULL
	CREATE INDEX IX_SaaS_TenantInvitation_Tenant
		ON SaaS.SaaS_TenantInvitation (TenantId) WHERE IsDeleted = 0;

-- 2. SaaS_TenantInvitationRole (pending role assignments) ──────────────────────
IF OBJECT_ID(N'SaaS.SaaS_TenantInvitationRole', N'U') IS NULL
CREATE TABLE SaaS.SaaS_TenantInvitationRole
(
	InvitationRoleId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_TenantInvitationRole PRIMARY KEY DEFAULT NEWID(),
	InvitationId      UNIQUEIDENTIFIER NOT NULL,
	RoleId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_TenantInvitationRole_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_TenantInvitationRole_IsDeleted DEFAULT 0,
	CONSTRAINT FK_SaaS_TenantInvitationRole_Invitation FOREIGN KEY (InvitationId) REFERENCES SaaS.SaaS_TenantInvitation (InvitationId),
	CONSTRAINT FK_SaaS_TenantInvitationRole_Role FOREIGN KEY (RoleId) REFERENCES SaaS.SaaS_Role (RoleId)
);

IF OBJECT_ID(N'UX_SaaS_TenantInvitationRole', N'UQ') IS NULL
	CREATE UNIQUE INDEX UX_SaaS_TenantInvitationRole
		ON SaaS.SaaS_TenantInvitationRole (InvitationId, RoleId) WHERE IsDeleted = 0;

-- 3. SaaS_Outbox (transactional, retry-safe email queue) ───────────────────────
IF OBJECT_ID(N'SaaS.SaaS_Outbox', N'U') IS NULL
CREATE TABLE SaaS.SaaS_Outbox
(
	OutboxId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SaaS_Outbox PRIMARY KEY DEFAULT NEWID(),
	MessageType       NVARCHAR(80) NOT NULL,
	PayloadJson       NVARCHAR(MAX) NOT NULL,
	StatusCode        NVARCHAR(40) NOT NULL CONSTRAINT DF_SaaS_Outbox_Status DEFAULT N'Pending',
	AttemptCount      INT NOT NULL CONSTRAINT DF_SaaS_Outbox_Attempts DEFAULT 0,
	MaxAttempts       INT NOT NULL CONSTRAINT DF_SaaS_Outbox_MaxAttempts DEFAULT 5,
	LastError         NVARCHAR(2000) NULL,
	NextAttemptUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_Outbox_NextAttempt DEFAULT SYSUTCDATETIME(),
	ProcessedAtUtc    DATETIME2 NULL,
	CreatedDateUtc    DATETIME2 NOT NULL CONSTRAINT DF_SaaS_Outbox_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId   UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc   DATETIME2 NULL,
	ModifiedByUserId  UNIQUEIDENTIFIER NULL,
	IsDeleted         BIT NOT NULL CONSTRAINT DF_SaaS_Outbox_IsDeleted DEFAULT 0
);

IF OBJECT_ID(N'IX_SaaS_Outbox_Pending', N'IX') IS NULL
	CREATE INDEX IX_SaaS_Outbox_Pending
		ON SaaS.SaaS_Outbox (StatusCode, NextAttemptUtc) WHERE IsDeleted = 0 AND StatusCode = N'Pending';

COMMIT TRANSACTION;
