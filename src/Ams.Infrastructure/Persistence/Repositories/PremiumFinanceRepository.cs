using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PremiumFinance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PremiumFinanceRepository(ISqlConnectionFactory connectionFactory) : IPremiumFinanceRepository
{
    private const string RequestColumns = "PremiumFinanceRequestId, TenantId, RequestNumber, SourceTypeCode, QuoteId, PolicyId, RenewalId, SubmissionId, AccountId, CarrierId, ProducerUserId, AssignedToUserId, InsuredName, AgencyName, ProducerName, CarrierName, PolicyOrQuoteNumber, LineOfBusiness, EffectiveDate, PremiumAmount, TaxAmount, FeeAmount, TotalCostAmount, RequestedDownPaymentAmount, RequestedInstallmentCount, StatusCode, PreferredFinanceCompanyId, SelectedQuoteOptionId, CustomerEmail, CustomerPhone, Notes, RequestedDateUtc, SubmittedDateUtc, CompletedDateUtc, CreatedDateUtc";

    private const string ProviderColumns = "FinanceCompanyId, TenantId, CompanyCode, CompanyName, ContactName, EmailAddress, PhoneNumber, RemittanceInstructions, ProviderKey, IntegrationLevelCode, WebsiteUrl, PortalUrl, SupportsQuotes, SupportsApplications, SupportsAgreements, SupportsPaymentSchedules, SupportsAccountStatus, SupportsPayoff, ExternalProviderId, IsActive";

    private const string AgreementColumns = "fa.FinanceAgreementId, fa.TenantId, fa.AgencyBillReceivableId, fa.FinanceCompanyId, fc.CompanyName AS FinanceCompanyName, fa.PremiumFinanceRequestId, fa.PremiumFinanceQuoteOptionId, fa.PolicyId, fa.QuoteId, fa.AccountId, fa.AgreementNumber, fa.OriginalPremiumAmount, fa.TaxAndFeeAmount, fa.FinancedAmount, fa.DownPaymentAmount, fa.AprPercent, fa.FinanceChargeAmount, fa.PaymentCount, fa.PaymentAmount, fa.NextPaymentDate, fa.FundingStatusCode, fa.ApplicationStatusCode, fa.SignatureStatusCode, fa.AccountStatusCode, fa.ProviderApplicationReference, fa.DocumentId, fa.ESignEnvelopeId, fa.ExpectedFundingDate, fa.FundedDate, fa.CancellationProtectionDate, fa.ApprovedDateUtc, fa.ActivatedDateUtc, fa.LastSynchronizedDateUtc, fa.PayoffAmount, fa.PayoffGoodThroughDate, fa.StatusCode, fa.CreatedDateUtc";

    public async Task<PremiumFinanceWorkbenchDto> GetWorkbenchAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
SELECT PremiumFinanceReferenceOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, ColorHex, IsTerminal, IsDefault, SortOrder
FROM Billing.PremiumFinanceReferenceOption
WHERE TenantId=@TenantId AND IsActive=1 AND IsDeleted=0 ORDER BY OptionGroupCode, SortOrder;

SELECT {ProviderColumns} FROM Billing.FinanceCompany
WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY IsActive DESC, CompanyName;

SELECT {RequestColumns} FROM Billing.PremiumFinanceRequest
WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;

SELECT qo.PremiumFinanceQuoteOptionId, qo.TenantId, qo.PremiumFinanceRequestId, qo.FinanceCompanyId,
       fc.CompanyName AS FinanceCompanyName, qo.ProviderQuoteReference, qo.OptionName, qo.DownPaymentPercent,
       qo.DownPaymentAmount, qo.AmountFinanced, qo.AprPercent, qo.FinanceChargeAmount, qo.PaymentCount,
       qo.PaymentAmount, qo.FirstPaymentDate, qo.QuoteExpirationDate, qo.StatusCode, qo.TermsSummary,
       qo.IsSelected, qo.SelectedDateUtc, qo.CreatedDateUtc
FROM Billing.PremiumFinanceQuoteOption qo
JOIN Billing.FinanceCompany fc ON fc.FinanceCompanyId=qo.FinanceCompanyId AND fc.TenantId=qo.TenantId AND fc.IsDeleted=0
WHERE qo.TenantId=@TenantId AND qo.IsDeleted=0 ORDER BY qo.CreatedDateUtc DESC;

SELECT {AgreementColumns}
FROM Billing.FinanceAgreement fa
JOIN Billing.FinanceCompany fc ON fc.FinanceCompanyId=fa.FinanceCompanyId AND fc.TenantId=fa.TenantId AND fc.IsDeleted=0
WHERE fa.TenantId=@TenantId AND fa.IsDeleted=0 AND fa.PremiumFinanceRequestId IS NOT NULL
ORDER BY fa.CreatedDateUtc DESC;

SELECT PremiumFinancePaymentScheduleId, TenantId, FinanceAgreementId, InstallmentNumber, DueDate, ScheduledAmount,
       PrincipalAmount, FinanceChargeAmount, PaidAmount, PaidDate, StatusCode, ProviderPaymentReference
FROM Billing.PremiumFinancePaymentSchedule
WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY DueDate, InstallmentNumber;

SELECT
 (SELECT COUNT(1) FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND IsDeleted=0) AS TotalRequests,
 (SELECT COUNT(1) FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND StatusCode IN(N'Draft',N'OptionsRequested') AND IsDeleted=0) AS OptionsPending,
 (SELECT COUNT(1) FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND StatusCode=N'PendingSignature' AND IsDeleted=0) AS PendingSignatures,
 (SELECT COUNT(1) FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND StatusCode=N'PendingApproval' AND IsDeleted=0) AS PendingApprovals,
 (SELECT COUNT(1) FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND StatusCode=N'Active' AND IsDeleted=0 AND PremiumFinanceRequestId IS NOT NULL) AS ActiveFinancing,
 (SELECT COUNT(1) FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND AccountStatusCode IN(N'PastDue',N'CancellationPending') AND IsDeleted=0) AS AttentionRequired,
 (SELECT COUNT(1) FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND RenewalId IS NOT NULL AND EffectiveDate<=DATEADD(day,90,CONVERT(date,SYSUTCDATETIME())) AND StatusCode NOT IN(N'Active',N'Cancelled',N'Declined') AND IsDeleted=0) AS RenewalsDue,
 COALESCE((SELECT SUM(FinancedAmount) FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND IsDeleted=0 AND PremiumFinanceRequestId IS NOT NULL),0) AS TotalAmountFinanced,
 COALESCE((SELECT SUM(ScheduledAmount-COALESCE(PaidAmount,0)) FROM Billing.PremiumFinancePaymentSchedule WHERE TenantId=@TenantId AND StatusCode<>N'Paid' AND IsDeleted=0),0) AS TotalOutstandingScheduled;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new PremiumFinanceWorkbenchDto(
            (await multi.ReadAsync<PremiumFinanceReferenceOptionDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceProviderDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceRequestDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceQuoteOptionDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceAgreementDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinancePaymentScheduleDto>()).AsList(),
            await multi.ReadSingleAsync<PremiumFinanceSummaryDto>());
    }

    public async Task<PremiumFinanceDetailDto?> GetDetailAsync(Guid tenantId, Guid premiumFinanceRequestId, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
SELECT {RequestColumns} FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
SELECT qo.PremiumFinanceQuoteOptionId, qo.TenantId, qo.PremiumFinanceRequestId, qo.FinanceCompanyId, fc.CompanyName AS FinanceCompanyName,
 qo.ProviderQuoteReference, qo.OptionName, qo.DownPaymentPercent, qo.DownPaymentAmount, qo.AmountFinanced, qo.AprPercent,
 qo.FinanceChargeAmount, qo.PaymentCount, qo.PaymentAmount, qo.FirstPaymentDate, qo.QuoteExpirationDate, qo.StatusCode,
 qo.TermsSummary, qo.IsSelected, qo.SelectedDateUtc, qo.CreatedDateUtc
FROM Billing.PremiumFinanceQuoteOption qo JOIN Billing.FinanceCompany fc ON fc.FinanceCompanyId=qo.FinanceCompanyId AND fc.TenantId=qo.TenantId
WHERE qo.TenantId=@TenantId AND qo.PremiumFinanceRequestId=@RequestId AND qo.IsDeleted=0 ORDER BY qo.IsSelected DESC, qo.PaymentAmount;
SELECT {AgreementColumns} FROM Billing.FinanceAgreement fa JOIN Billing.FinanceCompany fc ON fc.FinanceCompanyId=fa.FinanceCompanyId AND fc.TenantId=fa.TenantId
WHERE fa.TenantId=@TenantId AND fa.PremiumFinanceRequestId=@RequestId AND fa.IsDeleted=0;
SELECT ps.PremiumFinancePaymentScheduleId, ps.TenantId, ps.FinanceAgreementId, ps.InstallmentNumber, ps.DueDate, ps.ScheduledAmount,
 ps.PrincipalAmount, ps.FinanceChargeAmount, ps.PaidAmount, ps.PaidDate, ps.StatusCode, ps.ProviderPaymentReference
FROM Billing.PremiumFinancePaymentSchedule ps JOIN Billing.FinanceAgreement fa ON fa.FinanceAgreementId=ps.FinanceAgreementId AND fa.TenantId=ps.TenantId
WHERE ps.TenantId=@TenantId AND fa.PremiumFinanceRequestId=@RequestId AND ps.IsDeleted=0 ORDER BY ps.InstallmentNumber;
SELECT PremiumFinanceActivityId, TenantId, PremiumFinanceRequestId, FinanceAgreementId, ActivityTypeCode, Subject, Notes,
 OldStatusCode, NewStatusCode, ProviderReference, ActivityDateUtc, CreatedByName, CreatedByUserId
FROM Billing.PremiumFinanceActivity WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0 ORDER BY ActivityDateUtc DESC;
SELECT pfd.PremiumFinanceDocumentId, pfd.TenantId, pfd.PremiumFinanceRequestId, pfd.FinanceAgreementId, pfd.DocumentId,
 pfd.DocumentRoleCode, pfd.IsCurrent, d.FileName, d.DocumentTypeCode, pfd.CreatedDateUtc
FROM Billing.PremiumFinanceDocument pfd JOIN DMS.Document d ON d.DocumentId=pfd.DocumentId AND d.TenantId=pfd.TenantId AND d.IsDeleted=0
WHERE pfd.TenantId=@TenantId AND pfd.PremiumFinanceRequestId=@RequestId AND pfd.IsDeleted=0 ORDER BY pfd.CreatedDateUtc DESC;
SELECT PremiumFinanceProviderTransactionId, TenantId, FinanceCompanyId, PremiumFinanceRequestId, FinanceAgreementId,
 OperationCode, CorrelationId, ExternalTransactionId, StatusCode, AttemptCount, ErrorDetails, CompletedDateUtc, CreatedDateUtc
FROM Billing.PremiumFinanceProviderTransaction WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, RequestId = premiumFinanceRequestId }, cancellationToken: cancellationToken));
        var request = await multi.ReadSingleOrDefaultAsync<PremiumFinanceRequestDto>();
        if (request is null) return null;
        return new PremiumFinanceDetailDto(
            request,
            (await multi.ReadAsync<PremiumFinanceQuoteOptionDto>()).AsList(),
            await multi.ReadSingleOrDefaultAsync<PremiumFinanceAgreementDto>(),
            (await multi.ReadAsync<PremiumFinancePaymentScheduleDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceActivityDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceDocumentDto>()).AsList(),
            (await multi.ReadAsync<PremiumFinanceProviderTransactionDto>()).AsList());
    }

    public async Task<PremiumFinanceSourceDto?> GetSourceAsync(Guid tenantId, string sourceTypeCode, Guid sourceId, CancellationToken cancellationToken = default)
    {
        var normalized = sourceTypeCode.Trim().ToLowerInvariant();
        var sql = normalized switch
        {
            "quote" => """
SELECT N'Quote' SourceTypeCode, q.QuoteId SourceId, s.TenantId, q.QuoteId, CAST(NULL AS UNIQUEIDENTIFIER) PolicyId,
 CAST(NULL AS UNIQUEIDENTIFIER) RenewalId, q.SubmissionId, s.AccountId, q.CarrierId, s.AssignedToUserId ProducerUserId,
 a.AccountName InsuredName, CAST(NULL AS NVARCHAR(200)) AgencyName, u.DisplayName ProducerName, c.CarrierName,
 q.QuoteNumber PolicyOrQuoteNumber, s.LineOfBusiness, CAST(COALESCE(q.EffectiveDate,s.EffectiveDate) AS date) EffectiveDate,
 q.AnnualPremium PremiumAmount, COALESCE(q.TaxesAndFees,0) TaxAmount, COALESCE(q.BrokerFee,0) FeeAmount,
 a.MainEmail CustomerEmail, a.MainPhone CustomerPhone,
 CAST(CASE WHEN (q.IsSelected=1 OR q.Status IN(N'Selected',N'Accepted',N'Bound')) AND q.AnnualPremium>0 THEN 1 ELSE 0 END AS bit) IsEligible,
 CASE WHEN q.AnnualPremium<=0 THEN N'Quote premium must be greater than zero.' WHEN q.IsSelected=0 AND q.Status NOT IN(N'Selected',N'Accepted',N'Bound') THEN N'Quote must be selected or accepted before financing.' END IneligibilityReason
FROM Submissions.Quote q JOIN Submissions.Submission s ON s.SubmissionId=q.SubmissionId AND s.TenantId=@TenantId AND s.IsDeleted=0
JOIN Client.Account a ON a.AccountId=s.AccountId AND a.TenantId=s.TenantId AND a.IsDeleted=0
LEFT JOIN Core.Carrier c ON c.CarrierId=q.CarrierId AND c.TenantId=s.TenantId AND c.IsDeleted=0
LEFT JOIN IAM.[User] u ON u.UserId=s.AssignedToUserId AND u.TenantId=s.TenantId AND u.IsDeleted=0
WHERE q.TenantId=@TenantId AND q.QuoteId=@SourceId AND q.IsDeleted=0;
""",
            "policy" => """
SELECT N'Policy' SourceTypeCode, bp.PolicyId SourceId, bp.TenantId, bp.QuoteId, bp.PolicyId,
 CAST(NULL AS UNIQUEIDENTIFIER) RenewalId, bp.SubmissionId, bp.AccountId, bp.CarrierId, pa.ProducerId ProducerUserId,
 a.AccountName InsuredName, CAST(NULL AS NVARCHAR(200)) AgencyName, pa.ProducerName, c.CarrierName,
 bp.PolicyNumber PolicyOrQuoteNumber, bp.LineOfBusiness, CAST(bp.EffectiveDate AS date) EffectiveDate,
 bp.AnnualPremium PremiumAmount, CAST(0 AS DECIMAL(18,2)) TaxAmount, CAST(0 AS DECIMAL(18,2)) FeeAmount,
 a.MainEmail CustomerEmail, a.MainPhone CustomerPhone, CAST(CASE WHEN bp.AnnualPremium>0 AND bp.Status<>N'Cancelled' THEN 1 ELSE 0 END AS bit) IsEligible,
 CASE WHEN bp.AnnualPremium<=0 THEN N'Policy premium must be greater than zero.' WHEN bp.Status=N'Cancelled' THEN N'Cancelled policies are not eligible for financing.' END IneligibilityReason
FROM Submissions.BoundPolicy bp JOIN Client.Account a ON a.AccountId=bp.AccountId AND a.TenantId=bp.TenantId AND a.IsDeleted=0
LEFT JOIN Core.Carrier c ON c.CarrierId=bp.CarrierId AND c.TenantId=bp.TenantId AND c.IsDeleted=0
OUTER APPLY(SELECT TOP 1 ProducerId,ProducerName FROM Policy.PolicyAssignment WHERE TenantId=bp.TenantId AND PolicyId=bp.PolicyId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC) pa
WHERE bp.TenantId=@TenantId AND bp.PolicyId=@SourceId AND bp.IsDeleted=0;
""",
            "renewal" => """
SELECT N'Renewal' SourceTypeCode, rc.RetentionCaseId SourceId, rc.TenantId, bp.QuoteId, rc.PolicyId,
 rc.RetentionCaseId RenewalId, bp.SubmissionId, rc.AccountId, bp.CarrierId, rc.AssignedToUserId ProducerUserId,
 rc.AccountName InsuredName, CAST(NULL AS NVARCHAR(200)) AgencyName, rc.Producer ProducerName, rc.Carrier CarrierName,
 rc.PolicyNumber PolicyOrQuoteNumber, rc.LineOfBusiness, rc.ExpirationDate EffectiveDate,
 rc.CurrentPremium PremiumAmount, CAST(0 AS DECIMAL(18,2)) TaxAmount, CAST(0 AS DECIMAL(18,2)) FeeAmount,
 a.MainEmail CustomerEmail, a.MainPhone CustomerPhone, CAST(CASE WHEN rc.CurrentPremium>0 THEN 1 ELSE 0 END AS bit) IsEligible,
 CASE WHEN rc.CurrentPremium<=0 THEN N'Renewal premium must be greater than zero.' END IneligibilityReason
FROM Renewal.RetentionCase rc JOIN Submissions.BoundPolicy bp ON bp.PolicyId=rc.PolicyId AND bp.TenantId=rc.TenantId AND bp.IsDeleted=0
JOIN Client.Account a ON a.AccountId=rc.AccountId AND a.TenantId=rc.TenantId AND a.IsDeleted=0
WHERE rc.TenantId=@TenantId AND rc.RetentionCaseId=@SourceId AND rc.IsDeleted=0;
""",
            _ => null
        };
        if (sql is null) return null;
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PremiumFinanceSourceDto>(new CommandDefinition(sql, new { TenantId = tenantId, SourceId = sourceId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateRequestAsync(CreatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF NOT EXISTS(SELECT 1 FROM Client.Account WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0) THROW 51000,N'Account not found for tenant.',1;
IF @PreferredFinanceCompanyId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceCompany WHERE TenantId=@TenantId AND FinanceCompanyId=@PreferredFinanceCompanyId AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Premium finance provider not found for tenant.',1;
DECLARE @Id UNIQUEIDENTIFIER=NEWID();
DECLARE @Number NVARCHAR(50)=CONCAT(N'PFR-',FORMAT(SYSUTCDATETIME(),N'yyyy'),N'-',RIGHT(N'000000'+CONVERT(NVARCHAR(6),NEXT VALUE FOR Billing.PremiumFinanceRequestNumberSequence),6));
INSERT Billing.PremiumFinanceRequest(PremiumFinanceRequestId,TenantId,RequestNumber,SourceTypeCode,QuoteId,PolicyId,RenewalId,SubmissionId,AccountId,CarrierId,ProducerUserId,AssignedToUserId,InsuredName,AgencyName,ProducerName,CarrierName,PolicyOrQuoteNumber,LineOfBusiness,EffectiveDate,PremiumAmount,TaxAmount,FeeAmount,RequestedDownPaymentAmount,RequestedInstallmentCount,StatusCode,PreferredFinanceCompanyId,CustomerEmail,CustomerPhone,Notes,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@Id,@TenantId,@Number,@SourceTypeCode,@QuoteId,@PolicyId,@RenewalId,@SubmissionId,@AccountId,@CarrierId,@ProducerUserId,@AssignedToUserId,@InsuredName,@AgencyName,@ProducerName,@CarrierName,@PolicyOrQuoteNumber,@LineOfBusiness,@EffectiveDate,@PremiumAmount,@TaxAmount,@FeeAmount,@RequestedDownPaymentAmount,@RequestedInstallmentCount,N'Draft',@PreferredFinanceCompanyId,@CustomerEmail,@CustomerPhone,@Notes,SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,ActivityTypeCode,Subject,Notes,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@Id,N'StatusChanged',N'Premium finance request created',@Notes,N'Draft',@CreatedByName,@CreatedByUserId);
COMMIT; SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task UpdateRequestAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF @PreferredFinanceCompanyId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceCompany WHERE TenantId=@TenantId AND FinanceCompanyId=@PreferredFinanceCompanyId AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Premium finance provider not found for tenant.',1;
UPDATE Billing.PremiumFinanceRequest SET AssignedToUserId=@AssignedToUserId,RequestedDownPaymentAmount=@RequestedDownPaymentAmount,
 RequestedInstallmentCount=@RequestedInstallmentCount,PreferredFinanceCompanyId=@PreferredFinanceCompanyId,CustomerEmail=@CustomerEmail,
 CustomerPhone=@CustomerPhone,Notes=@Notes,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId
WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 51000,N'Premium finance request not found for tenant.',1;
""";
        await ExecuteAsync(sql, new { RequestId = premiumFinanceRequestId, request.TenantId, request.AssignedToUserId, request.RequestedDownPaymentAmount, request.RequestedInstallmentCount, request.PreferredFinanceCompanyId, request.CustomerEmail, request.CustomerPhone, request.Notes, request.ModifiedByUserId }, cancellationToken);
    }

    public async Task UpdateRequestStatusAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Old NVARCHAR(50);
SELECT @Old=StatusCode FROM Billing.PremiumFinanceRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
IF @Old IS NULL THROW 51000,N'Premium finance request not found for tenant.',1;
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceReferenceOption WHERE TenantId=@TenantId AND OptionGroupCode=N'RequestStatus' AND OptionCode=@StatusCode AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Invalid premium finance status.',1;
UPDATE Billing.PremiumFinanceRequest SET StatusCode=@StatusCode,RequestedDateUtc=CASE WHEN @StatusCode=N'OptionsRequested' THEN COALESCE(RequestedDateUtc,SYSUTCDATETIME()) ELSE RequestedDateUtc END,SubmittedDateUtc=CASE WHEN @StatusCode=N'ApplicationSubmitted' THEN COALESCE(SubmittedDateUtc,SYSUTCDATETIME()) ELSE SubmittedDateUtc END,CompletedDateUtc=CASE WHEN @StatusCode IN(N'Active',N'Declined',N'Cancelled') THEN COALESCE(CompletedDateUtc,SYSUTCDATETIME()) ELSE CompletedDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,ActivityTypeCode,Subject,Notes,OldStatusCode,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@RequestId,N'StatusChanged',CONCAT(N'Status changed to ',@StatusCode),@Notes,@Old,@StatusCode,@ModifiedByName,@ModifiedByUserId);
COMMIT;
""";
        await ExecuteAsync(sql, new { RequestId = premiumFinanceRequestId, request.TenantId, request.StatusCode, request.Notes, request.ModifiedByUserId, request.ModifiedByName }, cancellationToken);
    }

    public async Task<Guid> AddQuoteOptionAsync(AddPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'Premium finance request not found for tenant.',1;
IF NOT EXISTS(SELECT 1 FROM Billing.FinanceCompany WHERE TenantId=@TenantId AND FinanceCompanyId=@FinanceCompanyId AND IsActive=1 AND SupportsQuotes=1 AND IsDeleted=0) THROW 51000,N'Quote-capable premium finance provider not found for tenant.',1;
DECLARE @Id UNIQUEIDENTIFIER=NEWID();
INSERT Billing.PremiumFinanceQuoteOption(PremiumFinanceQuoteOptionId,TenantId,PremiumFinanceRequestId,FinanceCompanyId,ProviderQuoteReference,OptionName,DownPaymentPercent,DownPaymentAmount,AmountFinanced,AprPercent,FinanceChargeAmount,PaymentCount,PaymentAmount,FirstPaymentDate,QuoteExpirationDate,StatusCode,TermsSummary,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@Id,@TenantId,@PremiumFinanceRequestId,@FinanceCompanyId,@ProviderQuoteReference,@OptionName,@DownPaymentPercent,@DownPaymentAmount,@AmountFinanced,@AprPercent,@FinanceChargeAmount,@PaymentCount,@PaymentAmount,@FirstPaymentDate,@QuoteExpirationDate,N'Received',@TermsSummary,SYSUTCDATETIME(),@CreatedByUserId,0);
UPDATE Billing.PremiumFinanceRequest SET StatusCode=N'OptionsReceived',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CreatedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId;
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,ActivityTypeCode,Subject,Notes,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@PremiumFinanceRequestId,N'ProviderContact',N'Financing option recorded',@TermsSummary,N'OptionsReceived',@CreatedByName,@CreatedByUserId);
COMMIT; SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task SelectQuoteOptionAsync(Guid premiumFinanceRequestId, SelectPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceQuoteOption WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND PremiumFinanceQuoteOptionId=@PremiumFinanceQuoteOptionId AND StatusCode=N'Received' AND IsDeleted=0) THROW 51000,N'Available financing option not found for tenant.',1;
UPDATE Billing.PremiumFinanceQuoteOption SET IsSelected=0,StatusCode=CASE WHEN StatusCode=N'Received' THEN N'NotSelected' ELSE StatusCode END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@SelectedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
UPDATE Billing.PremiumFinanceQuoteOption SET IsSelected=1,StatusCode=N'Selected',SelectedDateUtc=SYSUTCDATETIME(),SelectedByUserId=@SelectedByUserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@SelectedByUserId WHERE TenantId=@TenantId AND PremiumFinanceQuoteOptionId=@PremiumFinanceQuoteOptionId;
UPDATE Billing.PremiumFinanceRequest SET SelectedQuoteOptionId=@PremiumFinanceQuoteOptionId,StatusCode=N'OptionSelected',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@SelectedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId AND IsDeleted=0;
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,ActivityTypeCode,Subject,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@RequestId,N'StatusChanged',N'Financing option selected',N'OptionSelected',@SelectedByName,@SelectedByUserId);
COMMIT;
""";
        await ExecuteAsync(sql, new { RequestId = premiumFinanceRequestId, request.TenantId, request.PremiumFinanceQuoteOptionId, request.SelectedByUserId, request.SelectedByName }, cancellationToken);
    }

    public async Task<Guid> CreateAgreementAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN; DECLARE @OptionId UNIQUEIDENTIFIER,@PolicyId UNIQUEIDENTIFIER,@QuoteId UNIQUEIDENTIFIER,@AccountId UNIQUEIDENTIFIER,@Premium DECIMAL(18,2),@TaxFee DECIMAL(18,2),@ReceivableId UNIQUEIDENTIFIER;
SELECT @OptionId=SelectedQuoteOptionId,@PolicyId=PolicyId,@QuoteId=QuoteId,@AccountId=AccountId,@Premium=PremiumAmount,@TaxFee=TaxAmount+FeeAmount FROM Billing.PremiumFinanceRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0;
IF @OptionId IS NULL THROW 51000,N'A financing option must be selected before application submission.',1;
IF EXISTS(SELECT 1 FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'An agreement already exists for this request.',1;
SELECT TOP 1 @ReceivableId=AgencyBillReceivableId FROM Billing.AgencyBillReceivable WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;
DECLARE @Id UNIQUEIDENTIFIER=NEWID();
INSERT Billing.FinanceAgreement(FinanceAgreementId,TenantId,AgencyBillReceivableId,FinanceCompanyId,AgreementNumber,FinancedAmount,DownPaymentAmount,FundingStatusCode,ExpectedFundingDate,CancellationProtectionDate,StatusCode,PremiumFinanceRequestId,PremiumFinanceQuoteOptionId,PolicyId,QuoteId,AccountId,OriginalPremiumAmount,TaxAndFeeAmount,AprPercent,FinanceChargeAmount,PaymentCount,PaymentAmount,NextPaymentDate,ApplicationStatusCode,SignatureStatusCode,AccountStatusCode,ProviderApplicationReference,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT @Id,@TenantId,@ReceivableId,@FinanceCompanyId,@AgreementNumber,qo.AmountFinanced,qo.DownPaymentAmount,N'Pending',@ExpectedFundingDate,@CancellationProtectionDate,N'Pending',@PremiumFinanceRequestId,@OptionId,@PolicyId,@QuoteId,@AccountId,@Premium,@TaxFee,qo.AprPercent,qo.FinanceChargeAmount,qo.PaymentCount,qo.PaymentAmount,qo.FirstPaymentDate,N'Submitted',N'NotSent',N'Current',@ProviderApplicationReference,SYSUTCDATETIME(),@SubmittedByUserId,0 FROM Billing.PremiumFinanceQuoteOption qo WHERE qo.TenantId=@TenantId AND qo.PremiumFinanceQuoteOptionId=@OptionId AND qo.FinanceCompanyId=@FinanceCompanyId AND qo.IsSelected=1 AND qo.IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 51000,N'Selected option does not match the submitted provider.',1;
UPDATE Billing.PremiumFinanceRequest SET StatusCode=N'ApplicationSubmitted',SubmittedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@SubmittedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId;
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,FinanceAgreementId,ActivityTypeCode,Subject,NewStatusCode,ProviderReference,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@PremiumFinanceRequestId,@Id,N'ProviderContact',N'Application submission recorded',N'ApplicationSubmitted',@ProviderApplicationReference,@SubmittedByName,@SubmittedByUserId);
INSERT Billing.PremiumFinanceProviderTransaction(PremiumFinanceProviderTransactionId,TenantId,FinanceCompanyId,PremiumFinanceRequestId,FinanceAgreementId,OperationCode,StatusCode,CompletedDateUtc,CreatedByUserId) VALUES(NEWID(),@TenantId,@FinanceCompanyId,@PremiumFinanceRequestId,@Id,N'SubmitApplication',N'ManuallyRecorded',SYSUTCDATETIME(),@SubmittedByUserId);
COMMIT; SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task UpdateAgreementAsync(UpdatePremiumFinanceAgreementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN; DECLARE @RequestId UNIQUEIDENTIFIER,@OldStatus NVARCHAR(50);
SELECT @RequestId=PremiumFinanceRequestId,@OldStatus=StatusCode FROM Billing.FinanceAgreement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND IsDeleted=0;
IF @RequestId IS NULL THROW 51000,N'Premium finance agreement not found for tenant.',1;
UPDATE Billing.FinanceAgreement SET ApplicationStatusCode=COALESCE(@ApplicationStatusCode,ApplicationStatusCode),SignatureStatusCode=COALESCE(@SignatureStatusCode,SignatureStatusCode),FundingStatusCode=COALESCE(@FundingStatusCode,FundingStatusCode),AccountStatusCode=COALESCE(@AccountStatusCode,AccountStatusCode),StatusCode=COALESCE(@StatusCode,StatusCode),DocumentId=COALESCE(@DocumentId,DocumentId),ESignEnvelopeId=COALESCE(@ESignEnvelopeId,ESignEnvelopeId),FundedDate=COALESCE(@FundedDate,FundedDate),NextPaymentDate=COALESCE(@NextPaymentDate,NextPaymentDate),ApprovedDateUtc=COALESCE(@ApprovedDateUtc,ApprovedDateUtc),ActivatedDateUtc=COALESCE(@ActivatedDateUtc,ActivatedDateUtc),PayoffAmount=COALESCE(@PayoffAmount,PayoffAmount),PayoffGoodThroughDate=COALESCE(@PayoffGoodThroughDate,PayoffGoodThroughDate),LastSynchronizedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId;
DECLARE @RequestStatus NVARCHAR(50)=CASE WHEN @StatusCode=N'Active' THEN N'Active' WHEN @ApplicationStatusCode=N'Approved' THEN N'Approved' WHEN @SignatureStatusCode=N'Signed' THEN N'PendingApproval' WHEN @SignatureStatusCode=N'Sent' THEN N'PendingSignature' ELSE NULL END;
IF @RequestStatus IS NOT NULL UPDATE Billing.PremiumFinanceRequest SET StatusCode=@RequestStatus,CompletedDateUtc=CASE WHEN @RequestStatus=N'Active' THEN COALESCE(CompletedDateUtc,SYSUTCDATETIME()) ELSE CompletedDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@RequestId;
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,FinanceAgreementId,ActivityTypeCode,Subject,Notes,OldStatusCode,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@RequestId,@FinanceAgreementId,N'StatusChanged',N'Financing agreement updated',@Notes,@OldStatus,COALESCE(@StatusCode,@OldStatus),@ModifiedByName,@ModifiedByUserId);
COMMIT;
""";
        await ExecuteAsync(sql, request, cancellationToken);
    }

    public async Task ReplacePaymentScheduleAsync(ReplacePremiumFinancePaymentScheduleRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        var exists = await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND IsDeleted=0;", request, tx, cancellationToken: cancellationToken));
        if (exists != 1) throw new InvalidOperationException("Premium finance agreement not found for tenant.");
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Billing.PremiumFinancePaymentSchedule SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND IsDeleted=0;", request, tx, cancellationToken: cancellationToken));
        const string insert = """
INSERT Billing.PremiumFinancePaymentSchedule(PremiumFinancePaymentScheduleId,TenantId,FinanceAgreementId,InstallmentNumber,DueDate,ScheduledAmount,PrincipalAmount,FinanceChargeAmount,PaidAmount,PaidDate,StatusCode,ProviderPaymentReference,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(NEWID(),@TenantId,@FinanceAgreementId,@InstallmentNumber,@DueDate,@ScheduledAmount,@PrincipalAmount,@FinanceChargeAmount,@PaidAmount,@PaidDate,@StatusCode,@ProviderPaymentReference,SYSUTCDATETIME(),@ModifiedByUserId,0);
""";
        foreach (var item in request.Items)
            await cn.ExecuteAsync(new CommandDefinition(insert, new { request.TenantId, request.FinanceAgreementId, item.InstallmentNumber, item.DueDate, item.ScheduledAmount, item.PrincipalAmount, item.FinanceChargeAmount, item.PaidAmount, item.PaidDate, item.StatusCode, item.ProviderPaymentReference, request.ModifiedByUserId }, tx, cancellationToken: cancellationToken));
        var nextDue = request.Items.Where(x => x.StatusCode != "Paid").OrderBy(x => x.DueDate).Select(x => (DateOnly?)x.DueDate).FirstOrDefault();
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Billing.FinanceAgreement SET NextPaymentDate=@NextDue,LastSynchronizedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId;", new { request.TenantId, request.FinanceAgreementId, NextDue = nextDue, request.ModifiedByUserId }, tx, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition("INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,FinanceAgreementId,ActivityTypeCode,Subject,Notes,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@FinanceAgreementId,N'ProviderContact',N'Payment schedule synchronized',CONCAT(@Count,N' provider installment(s) recorded.'),@ModifiedByName,@ModifiedByUserId);", new { request.TenantId, request.FinanceAgreementId, Count = request.Items.Count, request.ModifiedByName, request.ModifiedByUserId }, tx, cancellationToken: cancellationToken));
        tx.Commit();
    }

    public async Task<Guid> AddActivityAsync(AddPremiumFinanceActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF @PremiumFinanceRequestId IS NULL AND @FinanceAgreementId IS NULL THROW 51000,N'Request or agreement is required.',1;
IF @PremiumFinanceRequestId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'Premium finance request not found for tenant.',1;
IF @FinanceAgreementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND IsDeleted=0) THROW 51000,N'Premium finance agreement not found for tenant.',1;
IF @PremiumFinanceRequestId IS NOT NULL AND @FinanceAgreementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'Agreement does not belong to the selected request.',1;
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceReferenceOption WHERE TenantId=@TenantId AND OptionGroupCode=N'ActivityType' AND OptionCode=@ActivityTypeCode AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Invalid premium finance activity type.',1;
DECLARE @Id UNIQUEIDENTIFIER=NEWID(); INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,FinanceAgreementId,ActivityTypeCode,Subject,Notes,ProviderReference,CreatedByName,CreatedByUserId) VALUES(@Id,@TenantId,@PremiumFinanceRequestId,@FinanceAgreementId,@ActivityTypeCode,@Subject,@Notes,@ProviderReference,@CreatedByName,@CreatedByUserId); SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<Guid> LinkDocumentAsync(LinkPremiumFinanceDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS(SELECT 1 FROM DMS.Document WHERE TenantId=@TenantId AND DocumentId=@DocumentId AND IsDeleted=0) THROW 51000,N'Document not found for tenant.',1;
IF @PremiumFinanceRequestId IS NULL AND @FinanceAgreementId IS NULL THROW 51000,N'Request or agreement is required.',1;
IF @PremiumFinanceRequestId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceRequest WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'Premium finance request not found for tenant.',1;
IF @FinanceAgreementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND IsDeleted=0) THROW 51000,N'Premium finance agreement not found for tenant.',1;
IF @PremiumFinanceRequestId IS NOT NULL AND @FinanceAgreementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Billing.FinanceAgreement WHERE TenantId=@TenantId AND FinanceAgreementId=@FinanceAgreementId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0) THROW 51000,N'Agreement does not belong to the selected request.',1;
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceReferenceOption WHERE TenantId=@TenantId AND OptionGroupCode=N'DocumentRole' AND OptionCode=@DocumentRoleCode AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Invalid premium finance document role.',1;
DECLARE @Id UNIQUEIDENTIFIER=NEWID(); UPDATE Billing.PremiumFinanceDocument SET IsCurrent=0,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CreatedByUserId WHERE TenantId=@TenantId AND DocumentRoleCode=@DocumentRoleCode AND ((@PremiumFinanceRequestId IS NOT NULL AND PremiumFinanceRequestId=@PremiumFinanceRequestId) OR (@FinanceAgreementId IS NOT NULL AND FinanceAgreementId=@FinanceAgreementId)) AND IsDeleted=0;
INSERT Billing.PremiumFinanceDocument(PremiumFinanceDocumentId,TenantId,PremiumFinanceRequestId,FinanceAgreementId,DocumentId,DocumentRoleCode,IsCurrent,CreatedByUserId) VALUES(@Id,@TenantId,@PremiumFinanceRequestId,@FinanceAgreementId,@DocumentId,@DocumentRoleCode,1,@CreatedByUserId); SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertProviderAsync(UpsertPremiumFinanceProviderRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS(SELECT 1 FROM Billing.PremiumFinanceReferenceOption WHERE TenantId=@TenantId AND OptionGroupCode=N'IntegrationLevel' AND OptionCode=@IntegrationLevelCode AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Invalid integration level.',1;
IF EXISTS(SELECT 1 FROM Billing.FinanceCompany WHERE TenantId=@TenantId AND CompanyCode=@CompanyCode AND IsDeleted=0 AND (@FinanceCompanyId IS NULL OR FinanceCompanyId<>@FinanceCompanyId)) THROW 51000,N'Premium finance provider code already exists for tenant.',1;
IF @ProviderKey IS NOT NULL AND EXISTS(SELECT 1 FROM Billing.FinanceCompany WHERE TenantId=@TenantId AND ProviderKey=@ProviderKey AND IsDeleted=0 AND (@FinanceCompanyId IS NULL OR FinanceCompanyId<>@FinanceCompanyId)) THROW 51000,N'Premium finance provider key already exists for tenant.',1;
DECLARE @Id UNIQUEIDENTIFIER=COALESCE(@FinanceCompanyId,NEWID());
IF @FinanceCompanyId IS NULL INSERT Billing.FinanceCompany(FinanceCompanyId,TenantId,CompanyCode,CompanyName,ContactName,EmailAddress,PhoneNumber,RemittanceInstructions,ProviderKey,IntegrationLevelCode,WebsiteUrl,PortalUrl,SupportsQuotes,SupportsApplications,SupportsAgreements,SupportsPaymentSchedules,SupportsAccountStatus,SupportsPayoff,ExternalProviderId,IsActive,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@CompanyCode,@CompanyName,@ContactName,@EmailAddress,@PhoneNumber,@RemittanceInstructions,@ProviderKey,@IntegrationLevelCode,@WebsiteUrl,@PortalUrl,@SupportsQuotes,@SupportsApplications,@SupportsAgreements,@SupportsPaymentSchedules,@SupportsAccountStatus,@SupportsPayoff,@ExternalProviderId,@IsActive,@UserId,0);
ELSE BEGIN UPDATE Billing.FinanceCompany SET CompanyCode=@CompanyCode,CompanyName=@CompanyName,ContactName=@ContactName,EmailAddress=@EmailAddress,PhoneNumber=@PhoneNumber,RemittanceInstructions=@RemittanceInstructions,ProviderKey=@ProviderKey,IntegrationLevelCode=@IntegrationLevelCode,WebsiteUrl=@WebsiteUrl,PortalUrl=@PortalUrl,SupportsQuotes=@SupportsQuotes,SupportsApplications=@SupportsApplications,SupportsAgreements=@SupportsAgreements,SupportsPaymentSchedules=@SupportsPaymentSchedules,SupportsAccountStatus=@SupportsAccountStatus,SupportsPayoff=@SupportsPayoff,ExternalProviderId=@ExternalProviderId,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND FinanceCompanyId=@FinanceCompanyId AND IsDeleted=0; IF @@ROWCOUNT<>1 THROW 51000,N'Premium finance provider not found for tenant.',1; END SELECT @Id;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task CancelRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE Billing.PremiumFinanceRequest SET StatusCode=N'Cancelled',CancelledDateUtc=SYSUTCDATETIME(),CompletedDateUtc=COALESCE(CompletedDateUtc,SYSUTCDATETIME()),CancellationReason=@Reason,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CancelledByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0 AND StatusCode NOT IN(N'Active',N'Cancelled');
IF @@ROWCOUNT<>1 THROW 51000,N'Premium finance request cannot be cancelled.',1;
UPDATE Billing.FinanceAgreement SET StatusCode=N'Cancelled',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CancelledByUserId WHERE TenantId=@TenantId AND PremiumFinanceRequestId=@PremiumFinanceRequestId AND IsDeleted=0 AND StatusCode<>N'Active';
INSERT Billing.PremiumFinanceActivity(PremiumFinanceActivityId,TenantId,PremiumFinanceRequestId,ActivityTypeCode,Subject,Notes,NewStatusCode,CreatedByName,CreatedByUserId) VALUES(NEWID(),@TenantId,@PremiumFinanceRequestId,N'StatusChanged',N'Premium finance request cancelled',@Reason,N'Cancelled',@CancelledByName,@CancelledByUserId);
COMMIT;
""";
        await ExecuteAsync(sql, request, cancellationToken);
    }

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
