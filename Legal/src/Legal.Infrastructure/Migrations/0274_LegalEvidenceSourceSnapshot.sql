SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_EvidenceSourceSnapshot', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_EvidenceSourceSnapshot
	(
		SourceSnapshotId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceSourceSnapshot PRIMARY KEY,
		DecisionEvidenceId UNIQUEIDENTIFIER NOT NULL,
		SourceProvider NVARCHAR(100) NULL,
		SourceVersion NVARCHAR(200) NULL,
		SourceRef NVARCHAR(1000) NULL,
		ContentHash CHAR(64) NOT NULL,
		PassageHash CHAR(64) NOT NULL,
		PassageRef NVARCHAR(500) NULL,
		ExtractionVersion NVARCHAR(100) NULL,
		RetrievedDateUtc DATETIME2 NOT NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceSourceSnapshot_Created DEFAULT SYSUTCDATETIME(),
		CONSTRAINT FK_Legal_EvidenceSourceSnapshot_Evidence FOREIGN KEY (DecisionEvidenceId) REFERENCES POLOXI.Legal_DecisionEvidence (DecisionEvidenceId)
	);
	CREATE UNIQUE INDEX UX_Legal_EvidenceSourceSnapshot_Identity
		ON POLOXI.Legal_EvidenceSourceSnapshot (DecisionEvidenceId, ContentHash, PassageHash);
END

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'DecisionEvidenceVerificationId') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD
		DecisionEvidenceVerificationId UNIQUEIDENTIFIER NULL,
		SourceSnapshotId UNIQUEIDENTIFIER NULL,
		PassageRef NVARCHAR(500) NULL;
END

IF OBJECT_ID(N'POLOXI.FK_Legal_DecisionEvidenceAttachment_Verification', N'F') IS NULL
	EXEC sys.sp_executesql N'ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Verification FOREIGN KEY (DecisionEvidenceVerificationId)
		REFERENCES POLOXI.Legal_DecisionEvidenceVerification (DecisionEvidenceVerificationId);';

IF OBJECT_ID(N'POLOXI.FK_Legal_DecisionEvidenceAttachment_Snapshot', N'F') IS NULL
	EXEC sys.sp_executesql N'ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Snapshot FOREIGN KEY (SourceSnapshotId)
		REFERENCES POLOXI.Legal_EvidenceSourceSnapshot (SourceSnapshotId);';

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceVerification', N'SourceSnapshotId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceVerification ADD
		SourceSnapshotId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceVerification', N'ProfileVersion') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceVerification ADD
		ProfileVersion INT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_ProfileVersion DEFAULT 1;
IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceVerification', N'VerificationVersion') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceVerification ADD
		VerificationVersion INT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_Version DEFAULT 1;

IF OBJECT_ID(N'POLOXI.FK_Legal_DecisionEvidenceVerification_Snapshot', N'F') IS NULL
	EXEC sys.sp_executesql N'ALTER TABLE POLOXI.Legal_DecisionEvidenceVerification ADD
		CONSTRAINT FK_Legal_DecisionEvidenceVerification_Snapshot FOREIGN KEY (SourceSnapshotId)
		REFERENCES POLOXI.Legal_EvidenceSourceSnapshot (SourceSnapshotId);';

IF NOT EXISTS (SELECT 1 FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceVerification')
		AND name = N'UX_Legal_DecisionEvidenceVerification_EvidenceVersion')
	EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_Legal_DecisionEvidenceVerification_EvidenceVersion
		ON POLOXI.Legal_DecisionEvidenceVerification (DecisionEvidenceId, VerificationVersion);';

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceVerificationFactor', N'PassageRef') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionEvidenceVerificationFactor ADD
		PassageRef NVARCHAR(500) NULL,
		VerifierId NVARCHAR(100) NULL,
		VerifierVersion NVARCHAR(100) NULL;
END

COMMIT TRANSACTION;