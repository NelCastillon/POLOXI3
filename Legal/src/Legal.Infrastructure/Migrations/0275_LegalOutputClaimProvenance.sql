SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionOutputClaimProvenance
	(
		OutputClaimProvenanceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionOutputClaimProvenance PRIMARY KEY,
		DecisionSessionId UNIQUEIDENTIFIER NOT NULL,
		ClaimId UNIQUEIDENTIFIER NOT NULL,
		SourceBranchId UNIQUEIDENTIFIER NULL,
		SourceCandidateId UNIQUEIDENTIFIER NULL,
		DecisionEvidenceId UNIQUEIDENTIFIER NULL,
		DecisionEvidenceAttachmentId UNIQUEIDENTIFIER NULL,
		DecisionEvidenceVerificationId UNIQUEIDENTIFIER NULL,
		SourceSnapshotId UNIQUEIDENTIFIER NULL,
		PassageRef NVARCHAR(500) NULL,
		DispositionCode NVARCHAR(20) NOT NULL,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionOutputClaimProvenance_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		CONSTRAINT FK_Legal_DecisionOutputClaimProvenance_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
		CONSTRAINT FK_Legal_DecisionOutputClaimProvenance_Evidence FOREIGN KEY (DecisionEvidenceId) REFERENCES POLOXI.Legal_DecisionEvidence (DecisionEvidenceId),
		CONSTRAINT FK_Legal_DecisionOutputClaimProvenance_Attachment FOREIGN KEY (DecisionEvidenceAttachmentId) REFERENCES POLOXI.Legal_DecisionEvidenceAttachment (DecisionEvidenceAttachmentId),
		CONSTRAINT FK_Legal_DecisionOutputClaimProvenance_Verification FOREIGN KEY (DecisionEvidenceVerificationId) REFERENCES POLOXI.Legal_DecisionEvidenceVerification (DecisionEvidenceVerificationId),
		CONSTRAINT FK_Legal_DecisionOutputClaimProvenance_Snapshot FOREIGN KEY (SourceSnapshotId) REFERENCES POLOXI.Legal_EvidenceSourceSnapshot (SourceSnapshotId),
		CONSTRAINT CK_Legal_DecisionOutputClaimProvenance_Disposition CHECK (DispositionCode IN (N'ALLOW', N'QUALIFY', N'SUPPRESS', N'CORRECT'))
	);
	CREATE INDEX IX_Legal_DecisionOutputClaimProvenance_SessionClaim
		ON POLOXI.Legal_DecisionOutputClaimProvenance (DecisionSessionId, ClaimId);
END

IF COL_LENGTH(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'ClaimText') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionOutputClaimProvenance ADD ClaimText NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'IsMaterial') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionOutputClaimProvenance ADD IsMaterial BIT NOT NULL
		CONSTRAINT DF_Legal_DecisionOutputClaimProvenance_IsMaterial DEFAULT 1;
IF COL_LENGTH(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'MappingStateCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionOutputClaimProvenance ADD MappingStateCode NVARCHAR(30) NOT NULL
		CONSTRAINT DF_Legal_DecisionOutputClaimProvenance_MappingState DEFAULT N'UNMAPPED';
IF COL_LENGTH(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'SourcePropositionId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionOutputClaimProvenance ADD SourcePropositionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'MappingReasonCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionOutputClaimProvenance ADD MappingReasonCode NVARCHAR(100) NULL;

COMMIT TRANSACTION;