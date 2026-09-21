SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_DecisionOutputClaimProvenance', N'U') IS NOT NULL
BEGIN
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
END

COMMIT TRANSACTION;
