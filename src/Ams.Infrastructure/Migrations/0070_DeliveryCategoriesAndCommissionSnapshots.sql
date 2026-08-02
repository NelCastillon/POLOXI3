SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Submissions.ProposalDeliveryProvider', N'DeliveryCategoryCode') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryProvider ADD DeliveryCategoryCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_Category DEFAULT N'All';
GO

IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'DeliveryCategoryCode') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD DeliveryCategoryCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Category DEFAULT N'Proposal';
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'DeliveryTypeCode') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD DeliveryTypeCode NVARCHAR(80) NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Type DEFAULT N'ProposalPackage';
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'EntityName') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD EntityName NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'EntityId') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD EntityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'AccountId') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD AccountId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'Subject') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD Subject NVARCHAR(300) NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'HtmlContent') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD HtmlContent NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'Submissions.ProposalDeliveryDispatch', N'DocumentId') IS NULL
	ALTER TABLE Submissions.ProposalDeliveryDispatch ADD DocumentId UNIQUEIDENTIFIER NULL;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ProposalDeliveryDispatch_Proposal')
	ALTER TABLE Submissions.ProposalDeliveryDispatch DROP CONSTRAINT FK_ProposalDeliveryDispatch_Proposal;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ProposalDeliveryDispatch_Submission')
	ALTER TABLE Submissions.ProposalDeliveryDispatch DROP CONSTRAINT FK_ProposalDeliveryDispatch_Submission;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name=N'ProposalId' AND is_nullable=0)
	ALTER TABLE Submissions.ProposalDeliveryDispatch ALTER COLUMN ProposalId UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name=N'SubmissionId' AND is_nullable=0)
	ALTER TABLE Submissions.ProposalDeliveryDispatch ALTER COLUMN SubmissionId UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name=N'ProposalVersionNumber' AND is_nullable=0)
	ALTER TABLE Submissions.ProposalDeliveryDispatch ALTER COLUMN ProposalVersionNumber INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ProposalDeliveryDispatch_Proposal')
	ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT FK_ProposalDeliveryDispatch_Proposal FOREIGN KEY(ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ProposalDeliveryDispatch_Submission')
	ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT FK_ProposalDeliveryDispatch_Submission FOREIGN KEY(SubmissionId) REFERENCES Submissions.Submission(SubmissionId);
GO

UPDATE Submissions.ProposalDeliveryDispatch
SET DeliveryCategoryCode=N'Proposal', DeliveryTypeCode=N'ProposalPackage', EntityName=N'Proposal', EntityId=ProposalId
WHERE DeliveryCategoryCode=N'Proposal' AND (EntityName IS NULL OR EntityId IS NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name=N'IX_ProposalDeliveryDispatch_Entity')
	CREATE INDEX IX_ProposalDeliveryDispatch_Entity ON Submissions.ProposalDeliveryDispatch(TenantId,DeliveryCategoryCode,DeliveryTypeCode,EntityName,EntityId,CreatedDateUtc DESC) WHERE IsDeleted=0;
GO

IF OBJECT_ID(N'Submissions.PolicyBindCommissionAllocationSnapshot', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.PolicyBindCommissionAllocationSnapshot
	(
		PolicyBindCommissionAllocationSnapshotId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyBindCommissionAllocationSnapshot PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL,
		CommissionPlanId UNIQUEIDENTIFIER NOT NULL,
		CommissionPlanVersionId UNIQUEIDENTIFIER NULL,
		CommissionSplitRuleId UNIQUEIDENTIFIER NULL,
		PayeeId UNIQUEIDENTIFIER NULL,
		PayeeUserId UNIQUEIDENTIFIER NULL,
		PayeeTypeCode NVARCHAR(50) NOT NULL,
		SplitPercent DECIMAL(9,4) NOT NULL,
		CommissionRatePct DECIMAL(9,4) NOT NULL,
		CommissionablePremium DECIMAL(18,2) NOT NULL,
		GrossCommissionAmount DECIMAL(18,2) NOT NULL,
		AllocationAmount DECIMAL(18,2) NOT NULL,
		SnapshotDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindCommissionSnapshot_Date DEFAULT SYSUTCDATETIME(),
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindCommissionSnapshot_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyBindCommissionSnapshot_Deleted DEFAULT 0,
		CONSTRAINT CK_PolicyBindCommissionSnapshot_Split CHECK(SplitPercent>0 AND SplitPercent<=100),
		CONSTRAINT CK_PolicyBindCommissionSnapshot_Amounts CHECK(CommissionablePremium>=0 AND GrossCommissionAmount>=0 AND AllocationAmount>=0)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Submissions.PolicyBindCommissionAllocationSnapshot') AND name=N'UX_PolicyBindCommissionSnapshot_Type')
	CREATE UNIQUE INDEX UX_PolicyBindCommissionSnapshot_Type ON Submissions.PolicyBindCommissionAllocationSnapshot(TenantId,PolicyBindTransactionId,PayeeTypeCode) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Submissions.PolicyBindCommissionAllocationSnapshot') AND name=N'IX_PolicyBindCommissionSnapshot_Transaction')
	CREATE INDEX IX_PolicyBindCommissionSnapshot_Transaction ON Submissions.PolicyBindCommissionAllocationSnapshot(TenantId,PolicyBindTransactionId) INCLUDE(PayeeId,PayeeUserId,SplitPercent,AllocationAmount) WHERE IsDeleted=0;
GO
