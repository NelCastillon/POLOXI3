SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI epistemic hardening: persist the four orthogonal epistemic dimensions (Research priority,
-- Evidence support, Mathematical verification, Falsification coverage) plus the derived state label on
-- every Math execution, so research priority and evidence are audited separately from mathematical
-- verification. Columns are added idempotently with safe defaults so existing rows remain valid.
IF OBJECT_ID(N'POLOXI.Legal_MathExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_MathExecution',N'ResearchPriority') IS NULL
		ALTER TABLE POLOXI.Legal_MathExecution
			ADD ResearchPriority DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_ResearchPriority DEFAULT 0;

	IF COL_LENGTH(N'POLOXI.Legal_MathExecution',N'EvidenceSupport') IS NULL
		ALTER TABLE POLOXI.Legal_MathExecution
			ADD EvidenceSupport DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_EvidenceSupport DEFAULT 0;

	IF COL_LENGTH(N'POLOXI.Legal_MathExecution',N'MathematicalVerification') IS NULL
		ALTER TABLE POLOXI.Legal_MathExecution
			ADD MathematicalVerification DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_MathematicalVerification DEFAULT 0;

	IF COL_LENGTH(N'POLOXI.Legal_MathExecution',N'FalsificationCoverage') IS NULL
		ALTER TABLE POLOXI.Legal_MathExecution
			ADD FalsificationCoverage DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_FalsificationCoverage DEFAULT 0;

	IF COL_LENGTH(N'POLOXI.Legal_MathExecution',N'EpistemicStateLabel') IS NULL
		ALTER TABLE POLOXI.Legal_MathExecution
			ADD EpistemicStateLabel NVARCHAR(60) NULL;
END;

COMMIT TRANSACTION;
