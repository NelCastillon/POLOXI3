SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Evidence-to-research-need provenance. This is deliberately separate from structural graph-edge
-- verification: an attempted source begins as PROPOSED_SUPPORT_FOR and gains decision authority only
-- after proposition-support verification finalizes the attachment state.
IF OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceAttachment', N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionEvidenceAttachment
	(
		DecisionEvidenceAttachmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionEvidenceAttachment PRIMARY KEY,
		DecisionSessionId            UNIQUEIDENTIFIER NOT NULL,
		DecisionResearchNeedId       UNIQUEIDENTIFIER NOT NULL,
		DecisionBranchId             UNIQUEIDENTIFIER NOT NULL,
		DecisionEvidenceId           UNIQUEIDENTIFIER NOT NULL,
		PropositionToResolve         NVARCHAR(MAX) NOT NULL,
		SupportStateCode             NVARCHAR(40) NOT NULL,
		AffectedGraphEdgeId          UNIQUEIDENTIFIER NULL,
		IsAuthoritative              BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceAttachment_Authoritative DEFAULT 0,
		AssessmentReason             NVARCHAR(1000) NULL,
		MatterId                     UNIQUEIDENTIFIER NULL,
		TenantId                     UNIQUEIDENTIFIER NOT NULL,
		CreatedDateUtc               DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceAttachment_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId              UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc              DATETIME2 NULL,
		ModifiedByUserId             UNIQUEIDENTIFIER NULL,
		IsDeleted                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceAttachment_Deleted DEFAULT 0,
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Session FOREIGN KEY (DecisionSessionId) REFERENCES POLOXI.Legal_DecisionSession (DecisionSessionId),
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Need FOREIGN KEY (DecisionResearchNeedId) REFERENCES POLOXI.Legal_DecisionResearchNeed (DecisionResearchNeedId),
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Branch FOREIGN KEY (DecisionBranchId) REFERENCES POLOXI.Legal_DecisionBranch (DecisionBranchId),
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Evidence FOREIGN KEY (DecisionEvidenceId) REFERENCES POLOXI.Legal_DecisionEvidence (DecisionEvidenceId),
		CONSTRAINT FK_Legal_DecisionEvidenceAttachment_Edge FOREIGN KEY (AffectedGraphEdgeId) REFERENCES POLOXI.Legal_DecisionGraphEdge (DecisionGraphEdgeId),
		CONSTRAINT CK_Legal_DecisionEvidenceAttachment_State CHECK (SupportStateCode IN
			(N'PROPOSED_SUPPORT_FOR', N'SUPPORTED_BY', N'PARTIALLY_SUPPORTED_BY', N'CONTRADICTED_BY', N'UNSUPPORTED'))
	);

	CREATE UNIQUE INDEX UX_Legal_DecisionEvidenceAttachment_NeedEvidence
		ON POLOXI.Legal_DecisionEvidenceAttachment (DecisionResearchNeedId, DecisionEvidenceId)
		WHERE IsDeleted = 0;
	CREATE INDEX IX_Legal_DecisionEvidenceAttachment_SessionState
		ON POLOXI.Legal_DecisionEvidenceAttachment (DecisionSessionId, SupportStateCode)
		WHERE IsDeleted = 0;
END

COMMIT TRANSACTION;