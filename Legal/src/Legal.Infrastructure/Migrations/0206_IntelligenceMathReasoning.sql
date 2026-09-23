-- POLOXI Scientific Reasoning — Mathematics V1 run persistence / audit trail.
-- One execution header row per solve, plus the deterministic verification verdicts (obligations) and
-- the verification-weighted ranked candidates. This makes every Math run reproducible and auditable:
-- nothing is silently accepted on LLM confidence — each obligation's Status/CounterexampleStatus and
-- the final convergence Outcome are recorded exactly as the deterministic verifier decided them.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- Math solve execution log (one row per SolveAsync run).
IF OBJECT_ID(N'POLOXI.Legal_MathExecution',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MathExecution
	(
		MathExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PoloxiMathExecution PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		UserId UNIQUEIDENTIFIER NOT NULL,
		ProblemText NVARCHAR(MAX) NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		OutcomeCode NVARCHAR(50) NOT NULL,
		VerificationStatusCode NVARCHAR(50) NOT NULL,
		FinalAnswer NVARCHAR(MAX) NULL,
		CanonicalAnswer NVARCHAR(MAX) NULL,
		SolutionSummary NVARCHAR(MAX) NULL,
		RemainingUncertainty NVARCHAR(MAX) NULL,
		DiscoveryConfidence DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_Discovery DEFAULT 0,
		SelfConsistencyAgreement DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_Agreement DEFAULT 0,
		ModelCode NVARCHAR(100) NULL,
		DurationMilliseconds BIGINT NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_PoloxiMathExecution_Deleted DEFAULT 0
	);
	CREATE INDEX IX_Legal_PoloxiMathExecution_Tenant ON POLOXI.Legal_MathExecution(TenantId,CreatedDateUtc DESC) WHERE IsDeleted=0;
	CREATE INDEX IX_Legal_PoloxiMathExecution_Correlation ON POLOXI.Legal_MathExecution(CorrelationId) WHERE IsDeleted=0;
END;

-- Per-obligation deterministic verification verdict (the audit trail proving acceptance authority).
IF OBJECT_ID(N'POLOXI.Legal_MathObligation',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MathObligation
	(
		MathObligationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PoloxiMathObligation PRIMARY KEY,
		MathExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_PoloxiMathObligation_Execution REFERENCES POLOXI.Legal_MathExecution(MathExecutionId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ObligationKey NVARCHAR(120) NOT NULL,
		Statement NVARCHAR(MAX) NOT NULL,
		VerificationMethodCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		CounterexampleStatusCode NVARCHAR(50) NOT NULL,
		DiscoveryConfidence DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathObligation_Discovery DEFAULT 0,
		VerificationNote NVARCHAR(MAX) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_PoloxiMathObligation_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_PoloxiMathObligation_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_PoloxiMathObligation_Deleted DEFAULT 0
	);
	CREATE INDEX IX_Legal_PoloxiMathObligation_Execution ON POLOXI.Legal_MathObligation(MathExecutionId,SortOrder) WHERE IsDeleted=0;
END;

-- Verification-weighted ranked candidates (competing strategies/answers, never deleted).
IF OBJECT_ID(N'POLOXI.Legal_MathCandidate',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MathCandidate
	(
		MathCandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_PoloxiMathCandidate PRIMARY KEY,
		MathExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_PoloxiMathCandidate_Execution REFERENCES POLOXI.Legal_MathExecution(MathExecutionId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CandidateKey NVARCHAR(120) NOT NULL,
		ObjectTypeCode NVARCHAR(50) NOT NULL,
		Name NVARCHAR(300) NOT NULL,
		Description NVARCHAR(MAX) NULL,
		DiscoveryConfidence DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_PoloxiMathCandidate_Discovery DEFAULT 0,
		VerificationStatusCode NVARCHAR(50) NOT NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_PoloxiMathCandidate_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_PoloxiMathCandidate_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_PoloxiMathCandidate_Deleted DEFAULT 0
	);
	CREATE INDEX IX_Legal_PoloxiMathCandidate_Execution ON POLOXI.Legal_MathCandidate(MathExecutionId,SortOrder) WHERE IsDeleted=0;
END;

COMMIT TRANSACTION;
