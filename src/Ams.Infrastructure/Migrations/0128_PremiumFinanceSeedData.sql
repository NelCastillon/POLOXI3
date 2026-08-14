-- 0128_PremiumFinanceSeedData.sql
-- Production-safe provider/reference configuration for active tenants and linked workflow
-- examples exclusively for the established AgencyBinder demo tenant. Idempotent.

SET NOCOUNT ON;

;WITH ProviderSeed AS
(
	SELECT * FROM (VALUES
		(N'AFCO', N'AFCO Credit Corporation', N'Manual', N'Manual', N'https://www.afco.com/', N'https://www.afco.com/', N'AFCO premium finance provider directory entry. Configure agency-specific contacts and portal access before use.', 10),
		(N'FIRST', N'FIRST Insurance Funding', N'Manual', N'Manual', N'https://www.firstinsurancefunding.com/', N'https://www.firstinsurancefunding.com/', N'FIRST Insurance Funding provider directory entry. Configure agency-specific contacts and portal access before use.', 20),
		(N'IPFS', N'Imperial PFS', N'Manual', N'Manual', N'https://www.ipfs.com/', N'https://www.ipfs.com/', N'Imperial PFS provider directory entry. Configure agency-specific contacts and portal access before use.', 30),
		(N'ASCEND', N'Ascend', N'Manual', N'Manual', N'https://www.useascend.com/', N'https://www.useascend.com/', N'Ascend provider directory entry. Configure agency-specific contacts and integration settings before use.', 40)
	) seed(CompanyCode, CompanyName, ProviderKey, IntegrationLevelCode, WebsiteUrl, PortalUrl, RemittanceInstructions, SortOrder)
)
INSERT INTO Billing.FinanceCompany
(
	FinanceCompanyId, TenantId, CompanyCode, CompanyName, RemittanceInstructions,
	ProviderKey, IntegrationLevelCode, WebsiteUrl, PortalUrl,
	SupportsQuotes, SupportsApplications, SupportsAgreements, SupportsPaymentSchedules,
	SupportsAccountStatus, SupportsPayoff, IsActive, CreatedDateUtc, IsDeleted
)
SELECT NEWID(), tenant.TenantId, seed.CompanyCode, seed.CompanyName, seed.RemittanceInstructions,
	seed.ProviderKey, seed.IntegrationLevelCode, seed.WebsiteUrl, seed.PortalUrl,
	1, 1, 1, 1, 1, 1, 1, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN ProviderSeed seed
WHERE tenant.IsDeleted = 0
  AND tenant.IsActive = 1
  AND NOT EXISTS
  (
	SELECT 1
	FROM Billing.FinanceCompany existing
	WHERE existing.TenantId = tenant.TenantId
	  AND existing.CompanyCode = seed.CompanyCode
	  AND existing.IsDeleted = 0
  );

;WITH ReferenceSeed AS
(
	SELECT * FROM (VALUES
		(N'ApplicationStatus', N'NotSubmitted', N'Not Submitted', N'Application has not been submitted to the provider.', N'#6c757d', 0, 1, 10),
		(N'ApplicationStatus', N'Submitted', N'Submitted', N'Application was submitted to the provider.', N'#0d6efd', 0, 0, 20),
		(N'ApplicationStatus', N'InReview', N'In Review', N'Provider is reviewing the application.', N'#fd7e14', 0, 0, 30),
		(N'ApplicationStatus', N'Approved', N'Approved', N'Provider approved the application.', N'#198754', 1, 0, 40),
		(N'ApplicationStatus', N'Declined', N'Declined', N'Provider declined the application.', N'#dc3545', 1, 0, 50),
		(N'ApplicationStatus', N'Withdrawn', N'Withdrawn', N'Application was withdrawn before completion.', N'#6c757d', 1, 0, 60),
		(N'FundingStatus', N'Pending', N'Pending', N'Funding has not yet been confirmed.', N'#ffc107', 0, 1, 10),
		(N'FundingStatus', N'Scheduled', N'Scheduled', N'Provider scheduled the funding transaction.', N'#0dcaf0', 0, 0, 20),
		(N'FundingStatus', N'Funded', N'Funded', N'Provider confirmed funding.', N'#198754', 1, 0, 30),
		(N'FundingStatus', N'Failed', N'Failed', N'Funding could not be completed.', N'#dc3545', 1, 0, 40),
		(N'DocumentRole', N'Application', N'Finance Application', N'Application package submitted for financing.', N'#0d6efd', 0, 1, 10),
		(N'DocumentRole', N'Quote', N'Provider Quote', N'Provider financing quote or term sheet.', N'#0dcaf0', 0, 0, 20),
		(N'DocumentRole', N'Agreement', N'Finance Agreement', N'Premium finance agreement.', N'#6f42c1', 0, 0, 30),
		(N'DocumentRole', N'SignedAgreement', N'Signed Agreement', N'Customer-signed premium finance agreement.', N'#198754', 0, 0, 40),
		(N'DocumentRole', N'PaymentSchedule', N'Payment Schedule', N'Provider installment schedule.', N'#fd7e14', 0, 0, 50),
		(N'DocumentRole', N'Notice', N'Provider Notice', N'Notice received from the finance provider.', N'#dc3545', 0, 0, 60),
		(N'ProviderOperation', N'GetQuote', N'Get Quote', N'Request financing terms from a provider.', N'#0d6efd', 0, 1, 10),
		(N'ProviderOperation', N'SubmitApplication', N'Submit Application', N'Submit a financing application.', N'#6f42c1', 0, 0, 20),
		(N'ProviderOperation', N'GetApplicationStatus', N'Get Application Status', N'Retrieve current application status.', N'#0dcaf0', 0, 0, 30),
		(N'ProviderOperation', N'GetAgreement', N'Get Agreement', N'Retrieve the provider agreement.', N'#fd7e14', 0, 0, 40),
		(N'ProviderOperation', N'GetPaymentSchedule', N'Get Payment Schedule', N'Retrieve the installment schedule.', N'#198754', 0, 0, 50),
		(N'ProviderOperation', N'GetAccountStatus', N'Get Account Status', N'Retrieve servicing account status.', N'#198754', 0, 0, 60),
		(N'ProviderOperation', N'GetPayoff', N'Get Payoff', N'Retrieve payoff amount and good-through date.', N'#20c997', 0, 0, 70),
		(N'ProviderOperation', N'CancelRequest', N'Cancel Request', N'Cancel or withdraw a finance request.', N'#dc3545', 0, 0, 80),
		(N'ProviderTransactionStatus', N'Pending', N'Pending', N'Provider operation is waiting to be completed.', N'#ffc107', 0, 1, 10),
		(N'ProviderTransactionStatus', N'Completed', N'Completed', N'Provider operation completed successfully.', N'#198754', 1, 0, 20),
		(N'ProviderTransactionStatus', N'Failed', N'Failed', N'Provider operation failed and requires attention.', N'#dc3545', 1, 0, 30),
		(N'ProviderTransactionStatus', N'ManualActionRequired', N'Manual Action Required', N'Agency staff must complete the operation in the provider portal.', N'#fd7e14', 0, 0, 40),
		(N'ActivityType', N'QuoteRequested', N'Quote Requested', N'Agency requested premium finance terms.', N'#0d6efd', 0, 0, 60),
		(N'ActivityType', N'OptionSelected', N'Option Selected', N'Customer or agency selected a financing option.', N'#6f42c1', 0, 0, 70),
		(N'ActivityType', N'ApplicationSubmitted', N'Application Submitted', N'Financing application was submitted.', N'#fd7e14', 0, 0, 80),
		(N'ActivityType', N'AgreementSigned', N'Agreement Signed', N'Customer signed the financing agreement.', N'#198754', 0, 0, 90),
		(N'ActivityType', N'FundingConfirmed', N'Funding Confirmed', N'Provider funding was confirmed.', N'#20c997', 0, 0, 100),
		(N'ActivityType', N'PaymentStatusChanged', N'Payment Status Changed', N'An installment status changed.', N'#0dcaf0', 0, 0, 110)
	) seed(OptionGroupCode, OptionCode, DisplayName, Description, ColorHex, IsTerminal, IsDefault, SortOrder)
)
INSERT INTO Billing.PremiumFinanceReferenceOption
(
	TenantId, OptionGroupCode, OptionCode, DisplayName, Description, ColorHex,
	IsTerminal, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted
)
SELECT tenant.TenantId, seed.OptionGroupCode, seed.OptionCode, seed.DisplayName, seed.Description,
	seed.ColorHex, seed.IsTerminal, seed.IsDefault, 1, seed.SortOrder, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN ReferenceSeed seed
WHERE tenant.IsDeleted = 0
  AND tenant.IsActive = 1
  AND NOT EXISTS
  (
	SELECT 1
	FROM Billing.PremiumFinanceReferenceOption existing
	WHERE existing.TenantId = tenant.TenantId
	  AND existing.OptionGroupCode = seed.OptionGroupCode
	  AND existing.OptionCode = seed.OptionCode
  );

DECLARE @DemoTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @DemoAccountId UNIQUEIDENTIFIER =
(
	SELECT TOP (1) AccountId
	FROM Client.Account
	WHERE TenantId = @DemoTenantId AND IsDeleted = 0
	ORDER BY CreatedDateUtc, AccountId
);
DECLARE @DemoUserId UNIQUEIDENTIFIER =
(
	SELECT TOP (1) UserId
	FROM IAM.[User]
	WHERE TenantId = @DemoTenantId AND IsDeleted = 0
	ORDER BY CreatedDateUtc, UserId
);
DECLARE @AfcoId UNIQUEIDENTIFIER =
(
	SELECT TOP (1) FinanceCompanyId
	FROM Billing.FinanceCompany
	WHERE TenantId = @DemoTenantId AND CompanyCode = N'AFCO' AND IsDeleted = 0
);
DECLARE @FirstId UNIQUEIDENTIFIER =
(
	SELECT TOP (1) FinanceCompanyId
	FROM Billing.FinanceCompany
	WHERE TenantId = @DemoTenantId AND CompanyCode = N'FIRST' AND IsDeleted = 0
);

DECLARE @DraftRequestId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000001';
DECLARE @OptionsRequestId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000002';
DECLARE @ActiveRequestId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000003';
DECLARE @AfcoOptionId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000011';
DECLARE @FirstOptionId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000012';
DECLARE @ActiveOptionId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000013';
DECLARE @AgreementId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000021';
DECLARE @ApplicationDocumentId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000031';
DECLARE @AgreementDocumentId UNIQUEIDENTIFIER = '12800000-0000-0000-0000-000000000032';

IF EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = @DemoTenantId AND IsDeleted = 0)
   AND @DemoAccountId IS NOT NULL
   AND @AfcoId IS NOT NULL
   AND @FirstId IS NOT NULL
BEGIN
	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceRequest WHERE PremiumFinanceRequestId = @DraftRequestId)
	BEGIN
		INSERT INTO Billing.PremiumFinanceRequest
		(
			PremiumFinanceRequestId, TenantId, RequestNumber, SourceTypeCode, AccountId,
			InsuredName, AgencyName, ProducerName, CarrierName, PolicyOrQuoteNumber,
			LineOfBusiness, EffectiveDate, PremiumAmount, TaxAmount, FeeAmount,
			RequestedDownPaymentAmount, RequestedInstallmentCount, StatusCode,
			PreferredFinanceCompanyId, CustomerEmail, CustomerPhone, Notes,
			CreatedDateUtc, CreatedByUserId, IsDeleted
		)
		VALUES
		(
			@DraftRequestId, @DemoTenantId, N'PF-DEMO-0001', N'Manual', @DemoAccountId,
			N'Pinnacle Brokers Co.', N'Demo Agency', N'Taylor Admin', N'Acme Insurance Company', N'QUOTE-PF-1001',
			N'Commercial Property', DATEADD(day, 30, CONVERT(date, SYSUTCDATETIME())), 24600.00, 738.00, 325.00,
			6500.00, 9, N'Draft', @AfcoId, N'finance@pinnacle.example', N'(555) 010-1200',
			N'Demo request ready for the agency to verify and request provider terms.',
			DATEADD(day, -2, SYSUTCDATETIME()), @DemoUserId, 0
		);
	END;

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceRequest WHERE PremiumFinanceRequestId = @OptionsRequestId)
	BEGIN
		INSERT INTO Billing.PremiumFinanceRequest
		(
			PremiumFinanceRequestId, TenantId, RequestNumber, SourceTypeCode, AccountId,
			InsuredName, AgencyName, ProducerName, CarrierName, PolicyOrQuoteNumber,
			LineOfBusiness, EffectiveDate, PremiumAmount, TaxAmount, FeeAmount,
			RequestedDownPaymentAmount, RequestedInstallmentCount, StatusCode,
			PreferredFinanceCompanyId, CustomerEmail, CustomerPhone, Notes, RequestedDateUtc,
			CreatedDateUtc, CreatedByUserId, IsDeleted
		)
		VALUES
		(
			@OptionsRequestId, @DemoTenantId, N'PF-DEMO-0002', N'Manual', @DemoAccountId,
			N'Pinnacle Brokers Co.', N'Demo Agency', N'Taylor Admin', N'Continental Casualty', N'QUOTE-PF-1002',
			N'General Liability', DATEADD(day, 21, CONVERT(date, SYSUTCDATETIME())), 48250.00, 1447.50, 525.00,
			12500.00, 10, N'OptionsReceived', @FirstId, N'finance@pinnacle.example', N'(555) 010-1200',
			N'Demo comparison containing terms from two premium finance providers.', DATEADD(day, -4, SYSUTCDATETIME()),
			DATEADD(day, -5, SYSUTCDATETIME()), @DemoUserId, 0
		);
	END;

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceRequest WHERE PremiumFinanceRequestId = @ActiveRequestId)
	BEGIN
		INSERT INTO Billing.PremiumFinanceRequest
		(
			PremiumFinanceRequestId, TenantId, RequestNumber, SourceTypeCode, AccountId,
			InsuredName, AgencyName, ProducerName, CarrierName, PolicyOrQuoteNumber,
			LineOfBusiness, EffectiveDate, PremiumAmount, TaxAmount, FeeAmount,
			RequestedDownPaymentAmount, RequestedInstallmentCount, StatusCode,
			PreferredFinanceCompanyId, CustomerEmail, CustomerPhone, Notes,
			RequestedDateUtc, SubmittedDateUtc, CompletedDateUtc,
			CreatedDateUtc, CreatedByUserId, IsDeleted
		)
		VALUES
		(
			@ActiveRequestId, @DemoTenantId, N'PF-DEMO-0003', N'Manual', @DemoAccountId,
			N'Pinnacle Brokers Co.', N'Demo Agency', N'Taylor Admin', N'National Indemnity', N'POL-PF-2026-1003',
			N'Commercial Auto', DATEADD(day, -45, CONVERT(date, SYSUTCDATETIME())), 36500.00, 1095.00, 410.00,
			9501.25, 9, N'Active', @AfcoId, N'finance@pinnacle.example', N'(555) 010-1200',
			N'Complete active-financing example with agreement, schedule, documents, and servicing activity.',
			DATEADD(day, -60, SYSUTCDATETIME()), DATEADD(day, -57, SYSUTCDATETIME()), DATEADD(day, -50, SYSUTCDATETIME()),
			DATEADD(day, -62, SYSUTCDATETIME()), @DemoUserId, 0
		);
	END;

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceQuoteOption WHERE PremiumFinanceQuoteOptionId = @AfcoOptionId)
		INSERT INTO Billing.PremiumFinanceQuoteOption
		(PremiumFinanceQuoteOptionId, TenantId, PremiumFinanceRequestId, FinanceCompanyId, ProviderQuoteReference, OptionName, DownPaymentPercent, DownPaymentAmount, AmountFinanced, AprPercent, FinanceChargeAmount, PaymentCount, PaymentAmount, FirstPaymentDate, QuoteExpirationDate, StatusCode, TermsSummary, IsSelected, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES (@AfcoOptionId, @DemoTenantId, @OptionsRequestId, @AfcoId, N'AFCO-DEMO-Q-1002', N'Lower down payment', 20.0000, 10044.50, 40178.00, 8.7500, 1946.00, 10, 4212.40, DATEADD(day, 51, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 14, CONVERT(date, SYSUTCDATETIME())), N'Received', N'20% down with ten monthly installments. Demonstration terms only.', 0, DATEADD(day, -3, SYSUTCDATETIME()), @DemoUserId, 0);

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceQuoteOption WHERE PremiumFinanceQuoteOptionId = @FirstOptionId)
		INSERT INTO Billing.PremiumFinanceQuoteOption
		(PremiumFinanceQuoteOptionId, TenantId, PremiumFinanceRequestId, FinanceCompanyId, ProviderQuoteReference, OptionName, DownPaymentPercent, DownPaymentAmount, AmountFinanced, AprPercent, FinanceChargeAmount, PaymentCount, PaymentAmount, FirstPaymentDate, QuoteExpirationDate, StatusCode, TermsSummary, IsSelected, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES (@FirstOptionId, @DemoTenantId, @OptionsRequestId, @FirstId, N'FIRST-DEMO-Q-1002', N'Lower total finance charge', 25.0000, 12555.63, 37666.87, 7.9500, 1658.34, 9, 4370.58, DATEADD(day, 51, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 14, CONVERT(date, SYSUTCDATETIME())), N'Received', N'25% down with nine monthly installments. Demonstration terms only.', 0, DATEADD(day, -3, SYSUTCDATETIME()), @DemoUserId, 0);

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceQuoteOption WHERE PremiumFinanceQuoteOptionId = @ActiveOptionId)
		INSERT INTO Billing.PremiumFinanceQuoteOption
		(PremiumFinanceQuoteOptionId, TenantId, PremiumFinanceRequestId, FinanceCompanyId, ProviderQuoteReference, OptionName, DownPaymentPercent, DownPaymentAmount, AmountFinanced, AprPercent, FinanceChargeAmount, PaymentCount, PaymentAmount, FirstPaymentDate, QuoteExpirationDate, StatusCode, TermsSummary, IsSelected, SelectedDateUtc, SelectedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES (@ActiveOptionId, @DemoTenantId, @ActiveRequestId, @AfcoId, N'AFCO-DEMO-Q-1003', N'Selected nine-payment plan', 25.0000, 9501.25, 28503.75, 8.2500, 1282.50, 9, 3309.58, DATEADD(day, -15, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -48, CONVERT(date, SYSUTCDATETIME())), N'Selected', N'Selected demonstration terms for the active account.', 1, DATEADD(day, -55, SYSUTCDATETIME()), @DemoUserId, DATEADD(day, -58, SYSUTCDATETIME()), @DemoUserId, 0);

	UPDATE Billing.PremiumFinanceRequest
	SET SelectedQuoteOptionId = @ActiveOptionId
	WHERE PremiumFinanceRequestId = @ActiveRequestId AND SelectedQuoteOptionId IS NULL;

	IF NOT EXISTS (SELECT 1 FROM Billing.FinanceAgreement WHERE FinanceAgreementId = @AgreementId)
		INSERT INTO Billing.FinanceAgreement
		(FinanceAgreementId, TenantId, AgencyBillReceivableId, FinanceCompanyId, AgreementNumber, FinancedAmount, DownPaymentAmount, FundingStatusCode, ExpectedFundingDate, FundedDate, CancellationProtectionDate, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted, PremiumFinanceRequestId, PremiumFinanceQuoteOptionId, AccountId, OriginalPremiumAmount, TaxAndFeeAmount, AprPercent, FinanceChargeAmount, PaymentCount, PaymentAmount, NextPaymentDate, ApplicationStatusCode, SignatureStatusCode, AccountStatusCode, ProviderApplicationReference, ApprovedDateUtc, ActivatedDateUtc, LastSynchronizedDateUtc, PayoffAmount, PayoffGoodThroughDate)
		VALUES (@AgreementId, @DemoTenantId, NULL, @AfcoId, N'PFA-DEMO-1003', 28503.75, 9501.25, N'Funded', DATEADD(day, -48, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -48, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -15, CONVERT(date, SYSUTCDATETIME())), N'Active', DATEADD(day, -55, SYSUTCDATETIME()), @DemoUserId, 0, @ActiveRequestId, @ActiveOptionId, @DemoAccountId, 36500.00, 1505.00, 8.2500, 1282.50, 9, 3309.58, DATEADD(day, 15, CONVERT(date, SYSUTCDATETIME())), N'Approved', N'Signed', N'Current', N'AFCO-DEMO-A-1003', DATEADD(day, -52, SYSUTCDATETIME()), DATEADD(day, -48, SYSUTCDATETIME()), DATEADD(hour, -6, SYSUTCDATETIME()), 23167.06, DATEADD(day, 15, CONVERT(date, SYSUTCDATETIME())));

	;WITH InstallmentSeed AS
	(
		SELECT * FROM (VALUES
			(1, -15, 3309.58, 3167.08, 142.50, 3309.58, -14, N'Paid'),
			(2,  15, 3309.58, 3182.92, 126.66, NULL, NULL, N'Scheduled'),
			(3,  45, 3309.58, 3198.83, 110.75, NULL, NULL, N'Scheduled'),
			(4,  75, 3309.58, 3214.82,  94.76, NULL, NULL, N'Scheduled'),
			(5, 105, 3309.58, 3230.89,  78.69, NULL, NULL, N'Scheduled'),
			(6, 135, 3309.58, 3247.04,  62.54, NULL, NULL, N'Scheduled'),
			(7, 165, 3309.58, 3263.27,  46.31, NULL, NULL, N'Scheduled'),
			(8, 195, 3309.58, 3279.59,  29.99, NULL, NULL, N'Scheduled'),
			(9, 225, 3309.58, 3295.96,  13.62, NULL, NULL, N'Scheduled')
		) seed(InstallmentNumber, DueOffset, ScheduledAmount, PrincipalAmount, FinanceChargeAmount, PaidAmount, PaidOffset, StatusCode)
	)
	INSERT INTO Billing.PremiumFinancePaymentSchedule
	(TenantId, FinanceAgreementId, InstallmentNumber, DueDate, ScheduledAmount, PrincipalAmount, FinanceChargeAmount, PaidAmount, PaidDate, StatusCode, ProviderPaymentReference, CreatedDateUtc, CreatedByUserId, IsDeleted)
	SELECT @DemoTenantId, @AgreementId, seed.InstallmentNumber, DATEADD(day, seed.DueOffset, CONVERT(date, SYSUTCDATETIME())), seed.ScheduledAmount, seed.PrincipalAmount, seed.FinanceChargeAmount, seed.PaidAmount, CASE WHEN seed.PaidOffset IS NULL THEN NULL ELSE DATEADD(day, seed.PaidOffset, CONVERT(date, SYSUTCDATETIME())) END, seed.StatusCode, CONCAT(N'AFCO-DEMO-PMT-', FORMAT(seed.InstallmentNumber, '00')), DATEADD(day, -48, SYSUTCDATETIME()), @DemoUserId, 0
	FROM InstallmentSeed seed
	WHERE NOT EXISTS (SELECT 1 FROM Billing.PremiumFinancePaymentSchedule existing WHERE existing.FinanceAgreementId = @AgreementId AND existing.InstallmentNumber = seed.InstallmentNumber AND existing.IsDeleted = 0);

	;WITH ActivitySeed AS
	(
		SELECT * FROM (VALUES
			(CAST('12800000-0000-0000-0000-000000000041' AS UNIQUEIDENTIFIER), @OptionsRequestId, CAST(NULL AS UNIQUEIDENTIFIER), N'QuoteRequested', N'Provider terms requested', N'Terms requested from AFCO and FIRST for comparison.', N'Draft', N'OptionsRequested', -4),
			(CAST('12800000-0000-0000-0000-000000000042' AS UNIQUEIDENTIFIER), @OptionsRequestId, CAST(NULL AS UNIQUEIDENTIFIER), N'ProviderContact', N'Financing options received', N'Two manual provider options were recorded for agency review.', N'OptionsRequested', N'OptionsReceived', -3),
			(CAST('12800000-0000-0000-0000-000000000043' AS UNIQUEIDENTIFIER), @ActiveRequestId, @AgreementId, N'ApplicationSubmitted', N'Application submitted to AFCO', N'Demo application package was recorded as submitted through the provider portal.', N'OptionSelected', N'ApplicationSubmitted', -57),
			(CAST('12800000-0000-0000-0000-000000000044' AS UNIQUEIDENTIFIER), @ActiveRequestId, @AgreementId, N'AgreementSigned', N'Finance agreement signed', N'Customer signature was received and the agreement was sent for approval.', N'PendingSignature', N'PendingApproval', -53),
			(CAST('12800000-0000-0000-0000-000000000045' AS UNIQUEIDENTIFIER), @ActiveRequestId, @AgreementId, N'FundingConfirmed', N'Premium funding confirmed', N'Provider confirmed funding and the account became active.', N'Approved', N'Active', -48),
			(CAST('12800000-0000-0000-0000-000000000046' AS UNIQUEIDENTIFIER), @ActiveRequestId, @AgreementId, N'PaymentStatusChanged', N'First installment paid', N'Provider servicing status confirms the first installment was paid.', N'Scheduled', N'Paid', -14)
		) seed(ActivityId, RequestId, AgreementId, ActivityTypeCode, Subject, Notes, OldStatusCode, NewStatusCode, DayOffset)
	)
	INSERT INTO Billing.PremiumFinanceActivity
	(PremiumFinanceActivityId, TenantId, PremiumFinanceRequestId, FinanceAgreementId, ActivityTypeCode, Subject, Notes, OldStatusCode, NewStatusCode, ActivityDateUtc, CreatedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
	SELECT seed.ActivityId, @DemoTenantId, seed.RequestId, seed.AgreementId, seed.ActivityTypeCode, seed.Subject, seed.Notes, seed.OldStatusCode, seed.NewStatusCode, DATEADD(day, seed.DayOffset, SYSUTCDATETIME()), N'Demo Seed', DATEADD(day, seed.DayOffset, SYSUTCDATETIME()), @DemoUserId, 0
	FROM ActivitySeed seed
	WHERE NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceActivity existing WHERE existing.PremiumFinanceActivityId = seed.ActivityId);

	IF NOT EXISTS (SELECT 1 FROM DMS.Document WHERE DocumentId = @ApplicationDocumentId)
		INSERT INTO DMS.Document (DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, Description, Tags, UploadedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES (@ApplicationDocumentId, @DemoTenantId, N'PremiumFinanceApplication', N'PremiumFinance', N'PremiumFinanceRequest', @ActiveRequestId, N'PF-DEMO-0003-Application.pdf', N'seed/premium-finance/PF-DEMO-0003-Application.pdf', N'application/pdf', 184320, 1, N'Active', N'Demo premium finance application metadata record.', N'premium-finance;demo;application', N'Demo Seed', DATEADD(day, -57, SYSUTCDATETIME()), @DemoUserId, 0);

	IF NOT EXISTS (SELECT 1 FROM DMS.Document WHERE DocumentId = @AgreementDocumentId)
		INSERT INTO DMS.Document (DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, Description, Tags, UploadedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES (@AgreementDocumentId, @DemoTenantId, N'PremiumFinanceAgreement', N'PremiumFinance', N'FinanceAgreement', @AgreementId, N'PFA-DEMO-1003-Signed.pdf', N'seed/premium-finance/PFA-DEMO-1003-Signed.pdf', N'application/pdf', 241664, 1, N'Active', N'Demo signed premium finance agreement metadata record.', N'premium-finance;demo;signed-agreement', N'Demo Seed', DATEADD(day, -53, SYSUTCDATETIME()), @DemoUserId, 0);

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceDocument WHERE PremiumFinanceDocumentId = '12800000-0000-0000-0000-000000000051')
		INSERT INTO Billing.PremiumFinanceDocument (PremiumFinanceDocumentId, TenantId, PremiumFinanceRequestId, FinanceAgreementId, DocumentId, DocumentRoleCode, IsCurrent, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES ('12800000-0000-0000-0000-000000000051', @DemoTenantId, @ActiveRequestId, @AgreementId, @ApplicationDocumentId, N'Application', 1, DATEADD(day, -57, SYSUTCDATETIME()), @DemoUserId, 0);

	IF NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceDocument WHERE PremiumFinanceDocumentId = '12800000-0000-0000-0000-000000000052')
		INSERT INTO Billing.PremiumFinanceDocument (PremiumFinanceDocumentId, TenantId, PremiumFinanceRequestId, FinanceAgreementId, DocumentId, DocumentRoleCode, IsCurrent, CreatedDateUtc, CreatedByUserId, IsDeleted)
		VALUES ('12800000-0000-0000-0000-000000000052', @DemoTenantId, @ActiveRequestId, @AgreementId, @AgreementDocumentId, N'SignedAgreement', 1, DATEADD(day, -53, SYSUTCDATETIME()), @DemoUserId, 0);

	;WITH TransactionSeed AS
	(
		SELECT * FROM (VALUES
			(CAST('12800000-0000-0000-0000-000000000061' AS UNIQUEIDENTIFIER), N'GetQuote', N'AFCO-DEMO-TXN-QUOTE', N'Completed', N'{"mode":"manual","requestNumber":"PF-DEMO-0003"}', N'{"providerQuoteReference":"AFCO-DEMO-Q-1003","result":"Terms recorded"}', -58),
			(CAST('12800000-0000-0000-0000-000000000062' AS UNIQUEIDENTIFIER), N'SubmitApplication', N'AFCO-DEMO-TXN-APP', N'Completed', N'{"mode":"manual","agreementNumber":"PFA-DEMO-1003"}', N'{"providerApplicationReference":"AFCO-DEMO-A-1003","status":"Approved"}', -57),
			(CAST('12800000-0000-0000-0000-000000000063' AS UNIQUEIDENTIFIER), N'GetAccountStatus', N'AFCO-DEMO-TXN-STATUS', N'Completed', N'{"mode":"manual","agreementNumber":"PFA-DEMO-1003"}', N'{"accountStatus":"Current","nextPaymentAmount":3309.58}', 0)
		) seed(TransactionId, OperationCode, ExternalTransactionId, StatusCode, RequestJson, ResponseJson, DayOffset)
	)
	INSERT INTO Billing.PremiumFinanceProviderTransaction
	(PremiumFinanceProviderTransactionId, TenantId, FinanceCompanyId, PremiumFinanceRequestId, FinanceAgreementId, OperationCode, CorrelationId, ExternalTransactionId, StatusCode, RequestPayloadJson, ResponsePayloadJson, AttemptCount, CompletedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
	SELECT seed.TransactionId, @DemoTenantId, @AfcoId, @ActiveRequestId, @AgreementId, seed.OperationCode, seed.TransactionId, seed.ExternalTransactionId, seed.StatusCode, seed.RequestJson, seed.ResponseJson, 1, DATEADD(day, seed.DayOffset, SYSUTCDATETIME()), DATEADD(day, seed.DayOffset, SYSUTCDATETIME()), @DemoUserId, 0
	FROM TransactionSeed seed
	WHERE NOT EXISTS (SELECT 1 FROM Billing.PremiumFinanceProviderTransaction existing WHERE existing.PremiumFinanceProviderTransactionId = seed.TransactionId);
END;
