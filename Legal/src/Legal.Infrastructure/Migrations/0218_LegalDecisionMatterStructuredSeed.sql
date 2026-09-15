SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal - populate the new structured metadata columns for the Acme v. Globex demo matter
-- (M1), decomposing the free-text Type/Jurisdiction/Posture values seeded in 0216 into the
-- structured decision-contract dimensions added in 0217. Legacy columns are left intact for
-- back-compat; this migration only UPDATEs the new structured columns, so it is safe to re-run.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @M1     UNIQUEIDENTIFIER = N'A1000000-0000-0000-0000-000000000001';

IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @M1 AND TenantId = @Tenant)
	UPDATE POLOXI.Legal_DecisionMatter
	   SET Subtype              = N'Contract / Commercial Dispute',
		   CourtSystem          = N'United States - State',
		   State                = N'California',
		   CourtLevel           = N'Superior Court',
		   County               = N'Los Angeles County',
		   GoverningLaw         = N'California',
		   MovingParty          = N'Acme',
		   RespondingParty      = N'Globex',
		   MotionTarget         = N'Contract claim (alternatively summary adjudication of the non-use covenant)',
		   RequestedDisposition = N'Judgment for Acme without trial',
		   ModifiedDateUtc      = SYSUTCDATETIME(),
		   ModifiedByUserId     = @User
	 WHERE DecisionMatterId = @M1 AND TenantId = @Tenant;

COMMIT TRANSACTION;
