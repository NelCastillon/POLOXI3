SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceVerification', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionEvidenceVerification
	(
		DecisionEvidenceVerificationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEvidenceVerification PRIMARY KEY,
		DecisionEvidenceId             UNIQUEIDENTIFIER NOT NULL,
		DecisionSessionId              UNIQUEIDENTIFIER NOT NULL,
		DecisionBranchId               UNIQUEIDENTIFIER NULL,
		SourceTypeCode                 NVARCHAR(40) NOT NULL,
		ProfileCode                    NVARCHAR(60) NOT NULL,
		DispositionCode                NVARCHAR(40) NOT NULL,
		IsVerified                     BIT NOT NULL,
		IsDecisionAuthorized           BIT NOT NULL,
		BlockingReasonsJson            NVARCHAR(MAX) NULL,
		MatterId                       UNIQUEIDENTIFIER NULL,
		TenantId                       UNIQUEIDENTIFIER NOT NULL,
		EvaluatedDateUtc               DATETIME2 NOT NULL,
		CreatedDateUtc                 DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId                UNIQUEIDENTIFIER NULL,
		IsDeleted                     BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_Deleted DEFAULT 0,
		CONSTRAINT FK_Legal_DecisionEvidenceVerification_Evidence FOREIGN KEY (DecisionEvidenceId) REFERENCES POLOXI.Legal_DecisionEvidence (DecisionEvidenceId),
		CONSTRAINT FK_Legal_DecisionEvidenceVerification_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
		CONSTRAINT CK_Legal_DecisionEvidenceVerification_SourceType CHECK (SourceTypeCode IN
			(N'UNKNOWN', N'CASE_LAW', N'STATUTE', N'REGULATION', N'ADMINISTRATIVE_AUTHORITY',
			 N'MATTER_DOCUMENT', N'DECLARATION', N'DEPOSITION', N'CONTRACT', N'CORRESPONDENCE', N'BUSINESS_RECORD',
			 N'SECONDARY_AUTHORITY', N'GOVERNMENT_DOCUMENT')),
		CONSTRAINT CK_Legal_DecisionEvidenceVerification_Disposition CHECK (DispositionCode IN
			(N'SUPPORTED', N'PARTIALLY_SUPPORTED', N'UNSUPPORTED', N'CONTRADICTED', N'UNVERIFIABLE', N'ERROR')),
		CONSTRAINT CK_Legal_DecisionEvidenceVerification_Authority CHECK (IsDecisionAuthorized = 0 OR IsVerified = 1)
	);

	CREATE UNIQUE INDEX UX_Legal_DecisionEvidenceVerification_Evidence
		ON POLOXI.Legal_DecisionEvidenceVerification (DecisionEvidenceId)
		WHERE IsDeleted = 0;
	CREATE INDEX IX_Legal_DecisionEvidenceVerification_Session
		ON POLOXI.Legal_DecisionEvidenceVerification (DecisionSessionId, IsDecisionAuthorized, DispositionCode)
		WHERE IsDeleted = 0;
END

IF OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceVerificationFactor', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionEvidenceVerificationFactor
	(
		DecisionEvidenceVerificationFactorId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEvidenceVerificationFactor PRIMARY KEY,
		DecisionEvidenceVerificationId       UNIQUEIDENTIFIER NOT NULL,
		FactorCode                            NVARCHAR(40) NOT NULL,
		StateCode                             NVARCHAR(30) NOT NULL,
		ReasonCode                            NVARCHAR(100) NOT NULL,
		Reason                                NVARCHAR(1000) NULL,
		VerifiedValue                         NVARCHAR(MAX) NULL,
		SourceRef                             NVARCHAR(MAX) NULL,
		SupportingPassage                     NVARCHAR(MAX) NULL,
		VerificationMethod                    NVARCHAR(100) NOT NULL,
		SupportedComponentsJson               NVARCHAR(MAX) NULL,
		UnsupportedComponentsJson             NVARCHAR(MAX) NULL,
		EvaluatedDateUtc                      DATETIME2 NOT NULL,
		TenantId                              UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc                        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerificationFactor_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId                       UNIQUEIDENTIFIER NULL,
		IsDeleted                            BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerificationFactor_Deleted DEFAULT 0,
		CONSTRAINT FK_Legal_DecisionEvidenceVerificationFactor_Run FOREIGN KEY (DecisionEvidenceVerificationId) REFERENCES POLOXI.Legal_DecisionEvidenceVerification (DecisionEvidenceVerificationId),
		CONSTRAINT CK_Legal_DecisionEvidenceVerificationFactor_Factor CHECK (FactorCode IN
			(N'IDENTITY', N'PROVENANCE', N'CITATION', N'PASSAGE', N'PROPOSITION_SUPPORT', N'STATEMENT_ROLE', N'HOLDING', N'AUTHORITY')),
		CONSTRAINT CK_Legal_DecisionEvidenceVerificationFactor_State CHECK (StateCode IN
			(N'NOT_EVALUATED', N'PASSED', N'FAILED', N'NOT_APPLICABLE', N'INCONCLUSIVE', N'ERROR',
			 N'SUPPORTED', N'PARTIALLY_SUPPORTED', N'UNSUPPORTED', N'CONTRADICTED', N'UNVERIFIABLE'))
	);

	CREATE UNIQUE INDEX UX_Legal_DecisionEvidenceVerificationFactor_RunFactor
		ON POLOXI.Legal_DecisionEvidenceVerificationFactor (DecisionEvidenceVerificationId, FactorCode)
		WHERE IsDeleted = 0;
END

COMMIT TRANSACTION;
