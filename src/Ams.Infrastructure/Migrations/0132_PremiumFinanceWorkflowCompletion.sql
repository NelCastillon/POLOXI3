-- 0132_PremiumFinanceWorkflowCompletion.sql
-- Premium Finance relationship, financial, lookup, and workflow-status completion. Idempotent.

SET NOCOUNT ON;

;WITH MissingManualSource AS
(
	SELECT tenant.TenantId
	FROM Core.Tenant tenant
	WHERE tenant.IsDeleted=0
	  AND NOT EXISTS
	  (
		SELECT 1 FROM Billing.PremiumFinanceReferenceOption existing
		WHERE existing.TenantId=tenant.TenantId
		  AND existing.OptionGroupCode=N'SourceType'
		  AND existing.OptionCode=N'Manual'
		  AND existing.IsDeleted=0
	  )
)
INSERT INTO Billing.PremiumFinanceReferenceOption
	(
		PremiumFinanceReferenceOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName,
		Description, IsTerminal, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
	)
	SELECT NEWID(), TenantId, N'SourceType', N'Manual', N'Legacy manual record',
		N'Historical or imported premium finance record without a linked quote, policy, or renewal source.',
		0, 0, 0, 90, SYSUTCDATETIME(), 0
	FROM MissingManualSource;

;WITH MissingStatus AS
(
	SELECT tenant.TenantId
	FROM Core.Tenant tenant
	WHERE tenant.IsDeleted=0
	  AND NOT EXISTS
	  (
		SELECT 1 FROM Billing.PremiumFinanceReferenceOption existing
		WHERE existing.TenantId=tenant.TenantId
		  AND existing.OptionGroupCode=N'ProviderTransactionStatus'
		  AND existing.OptionCode=N'ManuallyRecorded'
		  AND existing.IsDeleted=0
	  )
)
INSERT INTO Billing.PremiumFinanceReferenceOption
(
	PremiumFinanceReferenceOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName,
	Description, IsTerminal, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
)
SELECT NEWID(), TenantId, N'ProviderTransactionStatus', N'ManuallyRecorded', N'Manually Recorded',
	N'The provider operation was completed outside AgencyBinder and recorded by an agency user.',
	1, 0, 1, 50, SYSUTCDATETIME(), 0
FROM MissingStatus;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Billing.PremiumFinanceRequest') AND name=N'CK_PremiumFinanceRequest_DownPayment')
	ALTER TABLE Billing.PremiumFinanceRequest WITH CHECK ADD CONSTRAINT CK_PremiumFinanceRequest_DownPayment CHECK
	(
		RequestedDownPaymentAmount IS NULL
		OR (RequestedDownPaymentAmount >= 0 AND RequestedDownPaymentAmount <= TotalCostAmount)
	);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FinanceAgreement_PremiumFinanceRequest')
	ALTER TABLE Billing.FinanceAgreement WITH CHECK ADD CONSTRAINT FK_FinanceAgreement_PremiumFinanceRequest FOREIGN KEY(PremiumFinanceRequestId) REFERENCES Billing.PremiumFinanceRequest(PremiumFinanceRequestId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FinanceAgreement_PremiumFinanceQuoteOption')
	ALTER TABLE Billing.FinanceAgreement WITH CHECK ADD CONSTRAINT FK_FinanceAgreement_PremiumFinanceQuoteOption FOREIGN KEY(PremiumFinanceQuoteOptionId) REFERENCES Billing.PremiumFinanceQuoteOption(PremiumFinanceQuoteOptionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceActivity_Request')
	ALTER TABLE Billing.PremiumFinanceActivity WITH CHECK ADD CONSTRAINT FK_PremiumFinanceActivity_Request FOREIGN KEY(PremiumFinanceRequestId) REFERENCES Billing.PremiumFinanceRequest(PremiumFinanceRequestId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceActivity_Agreement')
	ALTER TABLE Billing.PremiumFinanceActivity WITH CHECK ADD CONSTRAINT FK_PremiumFinanceActivity_Agreement FOREIGN KEY(FinanceAgreementId) REFERENCES Billing.FinanceAgreement(FinanceAgreementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceDocument_Request')
	ALTER TABLE Billing.PremiumFinanceDocument WITH CHECK ADD CONSTRAINT FK_PremiumFinanceDocument_Request FOREIGN KEY(PremiumFinanceRequestId) REFERENCES Billing.PremiumFinanceRequest(PremiumFinanceRequestId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceDocument_Agreement')
	ALTER TABLE Billing.PremiumFinanceDocument WITH CHECK ADD CONSTRAINT FK_PremiumFinanceDocument_Agreement FOREIGN KEY(FinanceAgreementId) REFERENCES Billing.FinanceAgreement(FinanceAgreementId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceProviderTransaction_Company')
	ALTER TABLE Billing.PremiumFinanceProviderTransaction WITH CHECK ADD CONSTRAINT FK_PremiumFinanceProviderTransaction_Company FOREIGN KEY(FinanceCompanyId) REFERENCES Billing.FinanceCompany(FinanceCompanyId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceProviderTransaction_Request')
	ALTER TABLE Billing.PremiumFinanceProviderTransaction WITH CHECK ADD CONSTRAINT FK_PremiumFinanceProviderTransaction_Request FOREIGN KEY(PremiumFinanceRequestId) REFERENCES Billing.PremiumFinanceRequest(PremiumFinanceRequestId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PremiumFinanceProviderTransaction_Agreement')
	ALTER TABLE Billing.PremiumFinanceProviderTransaction WITH CHECK ADD CONSTRAINT FK_PremiumFinanceProviderTransaction_Agreement FOREIGN KEY(FinanceAgreementId) REFERENCES Billing.FinanceAgreement(FinanceAgreementId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceActivity') AND name=N'IX_PremiumFinanceActivity_Agreement')
	CREATE INDEX IX_PremiumFinanceActivity_Agreement ON Billing.PremiumFinanceActivity(TenantId,FinanceAgreementId,ActivityDateUtc DESC) WHERE FinanceAgreementId IS NOT NULL AND IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceDocument') AND name=N'IX_PremiumFinanceDocument_Request')
	CREATE INDEX IX_PremiumFinanceDocument_Request ON Billing.PremiumFinanceDocument(TenantId,PremiumFinanceRequestId,DocumentRoleCode,IsCurrent) WHERE PremiumFinanceRequestId IS NOT NULL AND IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceDocument') AND name=N'IX_PremiumFinanceDocument_Agreement')
	CREATE INDEX IX_PremiumFinanceDocument_Agreement ON Billing.PremiumFinanceDocument(TenantId,FinanceAgreementId,DocumentRoleCode,IsCurrent) WHERE FinanceAgreementId IS NOT NULL AND IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceProviderTransaction') AND name=N'IX_PremiumFinanceProviderTransaction_Request')
	CREATE INDEX IX_PremiumFinanceProviderTransaction_Request ON Billing.PremiumFinanceProviderTransaction(TenantId,PremiumFinanceRequestId,CreatedDateUtc DESC) WHERE PremiumFinanceRequestId IS NOT NULL AND IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceProviderTransaction') AND name=N'IX_PremiumFinanceProviderTransaction_Agreement')
	CREATE INDEX IX_PremiumFinanceProviderTransaction_Agreement ON Billing.PremiumFinanceProviderTransaction(TenantId,FinanceAgreementId,CreatedDateUtc DESC) WHERE FinanceAgreementId IS NOT NULL AND IsDeleted=0;
