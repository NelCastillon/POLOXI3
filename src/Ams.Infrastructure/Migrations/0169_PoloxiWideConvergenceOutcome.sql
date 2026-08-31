SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI Wide GRIP-style global-convergence observability: one row per valid branch per wide
-- execution recording whether the branch was supported by its first retrieval, recovered by
-- readmitting narrowed-away evidence, recovered by a deterministic alternate approved term,
-- or left unresolved. Written only by the /intelligence/search/poloxi_wide path.
IF OBJECT_ID(N'POLOXI.ExecutionBranchOutcome',N'U') IS NULL
CREATE TABLE POLOXI.ExecutionBranchOutcome
(
	ExecutionBranchOutcomeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_ExecutionBranchOutcome PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	PoloxiExecutionId UNIQUEIDENTIFIER NOT NULL,
	HierarchyBranchId UNIQUEIDENTIFIER NOT NULL,
	OutcomeCode NVARCHAR(40) NOT NULL,
	RawEvidenceCount INT NOT NULL,
	KeptEvidenceCount INT NOT NULL,
	RecoveredEvidenceCount INT NOT NULL,
	AlternateSearchText NVARCHAR(500) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_ExecutionBranchOutcome_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_ExecutionBranchOutcome_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_ExecutionBranchOutcome_Execution FOREIGN KEY(PoloxiExecutionId) REFERENCES POLOXI.Execution(PoloxiExecutionId),
	CONSTRAINT FK_POLOXI_ExecutionBranchOutcome_Branch FOREIGN KEY(HierarchyBranchId) REFERENCES POLOXI.HierarchyBranch(HierarchyBranchId),
	CONSTRAINT CK_POLOXI_ExecutionBranchOutcome_Code CHECK(OutcomeCode IN (N'SUPPORTED',N'RECOVERED_READMITTED',N'RECOVERED_ALTERNATE_TERM',N'UNRESOLVED')),
	CONSTRAINT CK_POLOXI_ExecutionBranchOutcome_Counts CHECK(RawEvidenceCount>=0 AND KeptEvidenceCount>=0 AND RecoveredEvidenceCount>=0)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.ExecutionBranchOutcome') AND name=N'IX_POLOXI_ExecutionBranchOutcome_Execution')
CREATE INDEX IX_POLOXI_ExecutionBranchOutcome_Execution ON POLOXI.ExecutionBranchOutcome(TenantId,PoloxiExecutionId) INCLUDE(OutcomeCode,KeptEvidenceCount,RecoveredEvidenceCount);

COMMIT TRANSACTION;
