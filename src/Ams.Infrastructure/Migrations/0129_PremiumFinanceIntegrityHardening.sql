-- 0129_PremiumFinanceIntegrityHardening.sql
-- Defensive uniqueness and relationship integrity for Premium Finance. Idempotent.

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.FinanceCompany') AND name=N'UX_FinanceCompany_Tenant_ProviderKey')
BEGIN
	;WITH DuplicateProviderKeys AS
	(
		SELECT FinanceCompanyId,
			ROW_NUMBER() OVER (PARTITION BY TenantId, ProviderKey ORDER BY CreatedDateUtc, FinanceCompanyId) AS DuplicateNumber
		FROM Billing.FinanceCompany
		WHERE ProviderKey IS NOT NULL AND IsDeleted=0
	)
	UPDATE company
	SET ProviderKey=NULL
	FROM Billing.FinanceCompany company
	INNER JOIN DuplicateProviderKeys duplicate ON duplicate.FinanceCompanyId=company.FinanceCompanyId
	WHERE duplicate.DuplicateNumber > 1;

	EXEC(N'CREATE UNIQUE INDEX UX_FinanceCompany_Tenant_ProviderKey ON Billing.FinanceCompany(TenantId,ProviderKey) WHERE ProviderKey IS NOT NULL AND IsDeleted=0');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceDocument') AND name=N'UX_PremiumFinanceDocument_Current_Request_Role')
	EXEC(N'CREATE UNIQUE INDEX UX_PremiumFinanceDocument_Current_Request_Role ON Billing.PremiumFinanceDocument(TenantId,PremiumFinanceRequestId,DocumentRoleCode) WHERE PremiumFinanceRequestId IS NOT NULL AND IsCurrent=1 AND IsDeleted=0');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'Billing.PremiumFinanceDocument') AND name=N'UX_PremiumFinanceDocument_Current_Agreement_Role')
	EXEC(N'CREATE UNIQUE INDEX UX_PremiumFinanceDocument_Current_Agreement_Role ON Billing.PremiumFinanceDocument(TenantId,FinanceAgreementId,DocumentRoleCode) WHERE FinanceAgreementId IS NOT NULL AND IsCurrent=1 AND IsDeleted=0');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Billing.PremiumFinanceActivity') AND name=N'CK_PremiumFinanceActivity_Parent')
	ALTER TABLE Billing.PremiumFinanceActivity WITH CHECK ADD CONSTRAINT CK_PremiumFinanceActivity_Parent CHECK (PremiumFinanceRequestId IS NOT NULL OR FinanceAgreementId IS NOT NULL);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Billing.PremiumFinanceProviderTransaction') AND name=N'CK_PremiumFinanceProviderTransaction_Parent')
	ALTER TABLE Billing.PremiumFinanceProviderTransaction WITH CHECK ADD CONSTRAINT CK_PremiumFinanceProviderTransaction_Parent CHECK (PremiumFinanceRequestId IS NOT NULL OR FinanceAgreementId IS NOT NULL);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Billing.PremiumFinancePaymentSchedule') AND name=N'CK_PremiumFinancePaymentSchedule_Paid')
	ALTER TABLE Billing.PremiumFinancePaymentSchedule WITH CHECK ADD CONSTRAINT CK_PremiumFinancePaymentSchedule_Paid CHECK
	(
		PaidAmount IS NULL OR PaidAmount <= ScheduledAmount
	);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'Billing.FinanceAgreement') AND name=N'CK_FinanceAgreement_PremiumFinanceAmounts')
	ALTER TABLE Billing.FinanceAgreement WITH CHECK ADD CONSTRAINT CK_FinanceAgreement_PremiumFinanceAmounts CHECK
	(
		PremiumFinanceRequestId IS NULL
		OR (FinancedAmount >= 0 AND DownPaymentAmount >= 0 AND (PayoffAmount IS NULL OR PayoffAmount >= 0))
	);
