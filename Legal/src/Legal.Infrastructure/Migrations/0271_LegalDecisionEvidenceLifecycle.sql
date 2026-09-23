SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Exact verifier outcome for diagnostics and audit. VerificationStatus remains the coarse
-- schema-compatible authority state; LifecycleState records the verifier's actual terminal rung.
IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidence', N'LifecycleState') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionEvidence
		ADD LifecycleState NVARCHAR(50) NULL;
END

COMMIT TRANSACTION;
