SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Chat Judz — persistent enterprise conversation history for /legal/chatjudz
-- Every turn (user + Judz) is persisted loss-free so the chat surface can list past conversations
-- (newest first) and reopen the exact transcript. A "conversation" is simply all messages sharing the
-- same ConversationId, ordered by (Sequence, CreatedDateUtc). Self-contained and isolated: no cross-
-- module FKs; MatterId is an optional tag only. Scoped to the running user via (TenantId, CreatedByUserId).
-- Standard base/audit fields included.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

IF OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage',N'U') IS NULL
CREATE TABLE POLOXI.Legal_ChatJudzMessage
(
	ChatJudzMessageId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_ChatJudzMessage PRIMARY KEY DEFAULT NEWID(),
	ConversationId      UNIQUEIDENTIFIER NOT NULL,
	Sequence            INT NOT NULL CONSTRAINT DF_Legal_ChatJudzMessage_Sequence DEFAULT 0,
	Role                NVARCHAR(20) NOT NULL,
	Content             NVARCHAR(MAX) NOT NULL,
	ModelCodeUsed       NVARCHAR(100) NULL,
	MatterId            UNIQUEIDENTIFIER NULL,
	TenantId            UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_ChatJudzMessage_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_ChatJudzMessage_IsDeleted DEFAULT 0,
	RowVersion          ROWVERSION
);

GO

-- Thread access path: all messages in a conversation for a user, chronological transcript.
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChatJudzMessage_Conversation' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage'))
	CREATE INDEX IX_Legal_ChatJudzMessage_Conversation
		ON POLOXI.Legal_ChatJudzMessage (TenantId, CreatedByUserId, ConversationId, Sequence, CreatedDateUtc)
		WHERE IsDeleted = 0;

GO

-- List access path: a user's conversations, newest activity first, tenant-scoped.
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_ChatJudzMessage_User' AND object_id=OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage'))
	CREATE INDEX IX_Legal_ChatJudzMessage_User
		ON POLOXI.Legal_ChatJudzMessage (TenantId, CreatedByUserId, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

COMMIT TRANSACTION;
