SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceVerification', N'RetrievedCount') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionEvidenceVerification ADD
		RetrievedCount INT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_RetrievedCount DEFAULT 0,
		PreScreenRejectedCount INT NOT NULL CONSTRAINT DF_Legal_DecisionEvidenceVerification_PreScreenRejected DEFAULT 0;
END

COMMIT TRANSACTION;