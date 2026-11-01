SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Continuous chat threads for /legal/search.
-- Every /legal/search run is already persisted loss-free into POLOXI.Legal_WideSearchRunResult
-- (migration 0413). To make a whole conversation reopenable, we tag each run with the ConversationId
-- of the thread it belongs to. A "thread" is simply all runs sharing the same ConversationId, ordered
-- by CreatedDateUtc. This reuses the proven, fail-soft capture path and keeps one source of truth —
-- no parallel conversation/turn tables. Isolated from Decision2.
-- ────────────────────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'ConversationId') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_WideSearchRunResult ADD ConversationId UNIQUEIDENTIFIER NULL;
END;

GO

-- Thread access path: all runs in a conversation for a user, oldest first (chronological transcript).
IF OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult',N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_WideSearchRunResult',N'ConversationId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_WideSearchRunResult_Conversation' AND object_id=OBJECT_ID(N'POLOXI.Legal_WideSearchRunResult'))
BEGIN
	EXEC(N'CREATE INDEX IX_Legal_WideSearchRunResult_Conversation ON POLOXI.Legal_WideSearchRunResult(TenantId,CreatedByUserId,ConversationId,CreatedDateUtc) WHERE IsDeleted=0;');
END;

COMMIT TRANSACTION;
