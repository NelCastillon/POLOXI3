-- ============================================================================
-- 0382: Per matter + user "last opened" review state.
--
-- WHY: "Since Your Last Review" must be computed from real timestamps — the
-- document/evidence changes that occurred AFTER the attorney last opened the
-- matter. This table persists the last-opened moment per (matter, user) so the
-- Command Center can compute genuine deltas instead of fabricating them.
--
--  POLOXI.Legal_MatterReviewState
--    One row per (matter, user). LastOpenedUtc is advanced each time the
--    attorney opens the Command Center; PreviousOpenedUtc retains the prior
--    value so a single build can diff against the review boundary before it
--    moves forward.
--
-- Standard base/audit fields included. Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI;');

IF OBJECT_ID(N'POLOXI.Legal_MatterReviewState', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterReviewState
	(
		MatterReviewStateId UNIQUEIDENTIFIER NOT NULL
			CONSTRAINT PK_Legal_MatterReviewState PRIMARY KEY
			CONSTRAINT DF_Legal_MatterReviewState_Id DEFAULT NEWID(),
		TenantId            UNIQUEIDENTIFIER NOT NULL,
		MatterId            UNIQUEIDENTIFIER NOT NULL,
		UserId              UNIQUEIDENTIFIER NOT NULL,
		LastOpenedUtc       DATETIME2        NOT NULL CONSTRAINT DF_Legal_MatterReviewState_Opened DEFAULT SYSUTCDATETIME(),
		PreviousOpenedUtc   DATETIME2        NULL,
		CreatedDateUtc      DATETIME2        NOT NULL CONSTRAINT DF_Legal_MatterReviewState_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId     UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc     DATETIME2        NULL,
		ModifiedByUserId    UNIQUEIDENTIFIER NULL,
		IsDeleted           BIT              NOT NULL CONSTRAINT DF_Legal_MatterReviewState_Deleted DEFAULT 0
	);

	CREATE UNIQUE INDEX UX_Legal_MatterReviewState_Matter_User
		ON POLOXI.Legal_MatterReviewState (TenantId, MatterId, UserId)
		WHERE IsDeleted = 0;
END;

COMMIT TRANSACTION;
GO
