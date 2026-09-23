SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource',N'DiscoveryMethodCode') IS NULL
		ALTER TABLE POLOXI.Legal_AuthoritySource ADD DiscoveryMethodCode NVARCHAR(40) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource',N'DiscoveryEvidenceUrl') IS NULL
		ALTER TABLE POLOXI.Legal_AuthoritySource ADD DiscoveryEvidenceUrl NVARCHAR(2000) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource',N'VerifiedDateUtc') IS NULL
		ALTER TABLE POLOXI.Legal_AuthoritySource ADD VerifiedDateUtc DATETIME2 NULL;
END;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySearchOperation',N'U') IS NOT NULL
AND OBJECT_ID(N'POLOXI.FK_Legal_AuthoritySearchOperation_Parent',N'F') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchOperation WITH CHECK
	ADD CONSTRAINT FK_Legal_AuthoritySearchOperation_Parent FOREIGN KEY(ParentOperationId)
	REFERENCES POLOXI.Legal_AuthoritySearchOperation(LegalSearchOperationId);

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySearchResult',N'U') IS NOT NULL
AND OBJECT_ID(N'POLOXI.FK_Legal_AuthoritySearchResult_Operation',N'F') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchResult WITH CHECK
	ADD CONSTRAINT FK_Legal_AuthoritySearchResult_Operation FOREIGN KEY(LegalSearchOperationId)
	REFERENCES POLOXI.Legal_AuthoritySearchOperation(LegalSearchOperationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthoritySearchPlan') AND name=N'IX_Legal_AuthoritySearchPlan_Need')
	CREATE INDEX IX_Legal_AuthoritySearchPlan_Need ON POLOXI.Legal_AuthoritySearchPlan(DecisionResearchNeedId,CreatedDateUtc) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthorityProviderAttempt') AND name=N'IX_Legal_AuthorityProviderAttempt_Plan')
	CREATE INDEX IX_Legal_AuthorityProviderAttempt_Plan ON POLOXI.Legal_AuthorityProviderAttempt(LegalSearchPlanId,LegalSearchOperationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_VerifiedLegalProposition') AND name=N'IX_Legal_VerifiedLegalProposition_Need')
	CREATE INDEX IX_Legal_VerifiedLegalProposition_Need ON POLOXI.Legal_VerifiedLegalProposition(DecisionResearchNeedId,DecisionBranchId,CreatedDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthorityDecisionImpact') AND name=N'IX_Legal_AuthorityDecisionImpact_Need')
	CREATE INDEX IX_Legal_AuthorityDecisionImpact_Need ON POLOXI.Legal_AuthorityDecisionImpact(DecisionResearchNeedId,CreatedDateUtc);

COMMIT TRANSACTION;
