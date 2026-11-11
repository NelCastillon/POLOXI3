SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ════════════════════════════════════════════════════════════════════════════════════════════════
-- Converge the run-history graph onto the Matter hub (enforce option A).
--
-- Every decision RESULT table already carries DecisionSessionId NOT NULL with an enforced FK to
-- POLOXI.Legal_DecisionSession, and Legal_DecisionSession.MatterId is FK'd to Legal_DecisionMatter.
-- Earlier migrations (0220/0221/0233) denormalized a convenience MatterId onto several result tables
-- but WITHOUT a foreign key, so those values can drift or dangle.
--
-- This migration makes the denormalized MatterId trustworthy and queryable:
--   1. Ensure a MatterId column exists on every session-backed result table.
--   2. Backfill / repair MatterId from the authoritative parent session (source of truth) so no row
--      can be orphaned — DecisionSessionId is always present and FK'd, so this is lossless.
--   3. Add a guarded FK MatterId -> Legal_DecisionMatter(DecisionMatterId) (WITH CHECK, trusted).
--   4. Add the case-wide access index (TenantId, MatterId, DecisionSessionId) WHERE IsDeleted = 0.
--
-- Chat Judz history (Legal_ChatJudzMessage) has no run/session; its MatterId is an optional tag. We
-- still enforce referential integrity (nullable FK), nulling out any tag that points at a non-existent
-- matter so a tagged matter is guaranteed real. All operations are idempotent and re-runnable.
-- ════════════════════════════════════════════════════════════════════════════════════════════════

-- ── Session-backed result tables that must converge on the Matter hub via their parent session. ─────
DECLARE @ResultTables TABLE (TableName SYSNAME PRIMARY KEY);
INSERT INTO @ResultTables (TableName) VALUES
	(N'Legal_DecisionCandidate'),
	(N'Legal_DecisionBranch'),
	(N'Legal_DecisionDependency'),
	(N'Legal_DecisionEvidence'),
	(N'Legal_DecisionFlipPoint'),
	(N'Legal_DecisionEliminationLedger'),
	(N'Legal_DecisionEvent'),
	(N'Legal_DecisionFactProposition'),
	(N'Legal_DecisionLegalProposition'),
	(N'Legal_DecisionLegalElement'),
	(N'Legal_DecisionReasoningStrategy'),
	(N'Legal_DecisionBurdenRule'),
	(N'Legal_DecisionProceduralConstraint'),
	(N'Legal_DecisionGraphEdge'),
	(N'Legal_DecisionDependencyEvent'),
	(N'Legal_DecisionRecompetition'),
	(N'Legal_DecisionResearchNeed'),
	(N'Legal_DecisionFrontierSnapshot'),
	(N'Legal_ClaimProposition');

DECLARE @tbl SYSNAME, @fk SYSNAME, @ix SYSNAME, @sql NVARCHAR(MAX);

DECLARE conv_cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @ResultTables;
OPEN conv_cur;
FETCH NEXT FROM conv_cur INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
	IF OBJECT_ID(N'POLOXI.' + @tbl, N'U') IS NOT NULL
	   AND COL_LENGTH(N'POLOXI.' + @tbl, N'DecisionSessionId') IS NOT NULL
	BEGIN
		-- 1. Ensure the denormalized MatterId column exists.
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'MatterId') IS NULL
		BEGIN
			SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD MatterId UNIQUEIDENTIFIER NULL;';
			EXEC sp_executesql @sql;
		END

		-- 2. Backfill / repair MatterId from the authoritative parent session (source of truth).
		SET @sql =
			N'UPDATE t SET t.MatterId = s.MatterId ' +
			N'FROM POLOXI.' + QUOTENAME(@tbl) + N' t ' +
			N'JOIN POLOXI.Legal_DecisionSession s ON s.DecisionSessionId = t.DecisionSessionId ' +
			N'WHERE s.MatterId IS NOT NULL AND (t.MatterId IS NULL OR t.MatterId <> s.MatterId);';
		EXEC sp_executesql @sql;

		-- Quarantine any MatterId that still points at a non-existent matter (cannot trust-check otherwise).
		SET @sql =
			N'UPDATE t SET t.MatterId = NULL ' +
			N'FROM POLOXI.' + QUOTENAME(@tbl) + N' t ' +
			N'WHERE t.MatterId IS NOT NULL ' +
			N'  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter m WHERE m.DecisionMatterId = t.MatterId);';
		EXEC sp_executesql @sql;

		-- 3. Add the trusted FK to the Matter hub (idempotent).
		SET @fk = N'FK_' + @tbl + N'_Matter';
		IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @fk)
		BEGIN
			SET @sql =
				N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) +
				N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@fk) +
				N' FOREIGN KEY (MatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId);';
			EXEC sp_executesql @sql;
		END

		-- 4. Case-wide access index: everything for a matter across all runs is a single seek.
		SET @ix = N'IX_' + @tbl + N'_Matter';
		IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @ix AND object_id = OBJECT_ID(N'POLOXI.' + @tbl))
		BEGIN
			SET @sql =
				N'CREATE INDEX ' + QUOTENAME(@ix) + N' ON POLOXI.' + QUOTENAME(@tbl) +
				N' (TenantId, MatterId, DecisionSessionId) WHERE IsDeleted = 0;';
			EXEC sp_executesql @sql;
		END
	END
	FETCH NEXT FROM conv_cur INTO @tbl;
END
CLOSE conv_cur;
DEALLOCATE conv_cur;

GO

-- ── Chat Judz history: optional matter tag — enforce referential integrity without a session link. ──
IF OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_ChatJudzMessage', N'MatterId') IS NOT NULL
BEGIN
	-- Quarantine tags that point at a non-existent matter (keep the message, drop the dangling tag).
	UPDATE m SET m.MatterId = NULL
	FROM POLOXI.Legal_ChatJudzMessage m
	WHERE m.MatterId IS NOT NULL
	  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter d WHERE d.DecisionMatterId = m.MatterId);

	IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_ChatJudzMessage_Matter')
		ALTER TABLE POLOXI.Legal_ChatJudzMessage
			WITH CHECK ADD CONSTRAINT FK_Legal_ChatJudzMessage_Matter
			FOREIGN KEY (MatterId) REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId);

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_ChatJudzMessage_Matter' AND object_id = OBJECT_ID(N'POLOXI.Legal_ChatJudzMessage'))
		CREATE INDEX IX_Legal_ChatJudzMessage_Matter
			ON POLOXI.Legal_ChatJudzMessage (TenantId, MatterId, ConversationId) WHERE IsDeleted = 0;
END

COMMIT TRANSACTION;
