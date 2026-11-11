SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Chat Judz — per-user selected model for /legal/chatjudz.
-- Holds the chat model the running user picked in the Chat Judz toolbar. DB is the source of truth:
-- the chat endpoint uses this ModelCode as the model override for subsequent turns. Exactly one active
-- row per (TenantId, CreatedByUserId); the selection is upserted when the user changes it. When no row
-- exists (or ModelCode is NULL) the controller falls back to the configured default feature-policy
-- route. Historical message ModelCodeUsed stays immutable, so changing the selection only affects
-- future turns. Standard base/audit fields included.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

IF OBJECT_ID(N'POLOXI.Legal_ChatJudzModelSetting',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ChatJudzModelSetting
(
	ChatJudzModelSettingId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ChatJudzModelSetting PRIMARY KEY DEFAULT NEWID(),
	ModelCode               NVARCHAR(100) NOT NULL,
	TenantId                UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_ChatJudzModelSetting_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_ChatJudzModelSetting_IsDeleted DEFAULT 0,
	RowVersion              ROWVERSION
);

GO

-- One active selection per user, tenant-scoped. Filtered unique index enforces single live row.
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzModelSetting',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Legal_ChatJudzModelSetting_User' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChatJudzModelSetting'))
	CREATE UNIQUE INDEX UX_Legal_ChatJudzModelSetting_User
		ON POLOXI.Legal_ChatJudzModelSetting (TenantId, CreatedByUserId)
		WHERE IsDeleted = 0;

COMMIT TRANSACTION;
