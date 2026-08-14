using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PremiumFinance;

public sealed record CreatePremiumFinanceRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(40)] string SourceTypeCode,
    Guid? QuoteId,
    Guid? PolicyId,
    Guid? RenewalId,
    Guid? SubmissionId,
    [property: Required] Guid AccountId,
    Guid? CarrierId,
    Guid? ProducerUserId,
    Guid? AssignedToUserId,
    [property: Required, StringLength(200)] string InsuredName,
    [property: StringLength(200)] string? AgencyName,
    [property: StringLength(200)] string? ProducerName,
    [property: StringLength(200)] string? CarrierName,
    [property: Required, StringLength(120)] string PolicyOrQuoteNumber,
    [property: Required, StringLength(160)] string LineOfBusiness,
    DateOnly EffectiveDate,
    [property: Range(typeof(decimal), "0.01", "9999999999999999")] decimal PremiumAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal TaxAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal FeeAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? RequestedDownPaymentAmount,
    [property: Range(1, 120)] int? RequestedInstallmentCount,
    Guid? PreferredFinanceCompanyId,
    [property: EmailAddress, StringLength(254)] string? CustomerEmail,
    [property: Phone, StringLength(50)] string? CustomerPhone,
    [property: StringLength(2000)] string? Notes,
    Guid? CreatedByUserId,
    [property: StringLength(200)] string? CreatedByName);

public sealed record UpdatePremiumFinanceRequest(
    [property: Required] Guid TenantId,
    Guid? AssignedToUserId,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? RequestedDownPaymentAmount,
    [property: Range(1, 120)] int? RequestedInstallmentCount,
    Guid? PreferredFinanceCompanyId,
    [property: EmailAddress, StringLength(254)] string? CustomerEmail,
    [property: Phone, StringLength(50)] string? CustomerPhone,
    [property: StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId);

public sealed record UpdatePremiumFinanceStatusRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(50)] string StatusCode,
    [property: StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId,
    [property: StringLength(200)] string? ModifiedByName);

public sealed record AddPremiumFinanceQuoteOptionRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid PremiumFinanceRequestId,
    [property: Required] Guid FinanceCompanyId,
    [property: StringLength(160)] string? ProviderQuoteReference,
    [property: Required, StringLength(160)] string OptionName,
    [property: Range(typeof(decimal), "0", "100")] decimal DownPaymentPercent,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal DownPaymentAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal AmountFinanced,
    [property: Range(typeof(decimal), "0", "100")] decimal AprPercent,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal FinanceChargeAmount,
    [property: Range(1, 120)] int PaymentCount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal PaymentAmount,
    DateOnly? FirstPaymentDate,
    DateOnly? QuoteExpirationDate,
    [property: StringLength(2000)] string? TermsSummary,
    Guid? CreatedByUserId,
    [property: StringLength(200)] string? CreatedByName);

public sealed record SelectPremiumFinanceQuoteOptionRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid PremiumFinanceQuoteOptionId,
    Guid? SelectedByUserId,
    [property: StringLength(200)] string? SelectedByName);

public sealed record SubmitPremiumFinanceApplicationRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid PremiumFinanceRequestId,
    [property: Required] Guid FinanceCompanyId,
    [property: Required, StringLength(100)] string AgreementNumber,
    [property: StringLength(160)] string? ProviderApplicationReference,
    DateOnly? ExpectedFundingDate,
    DateOnly? CancellationProtectionDate,
    Guid? SubmittedByUserId,
    [property: StringLength(200)] string? SubmittedByName);

public sealed record UpdatePremiumFinanceAgreementRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid FinanceAgreementId,
    [property: StringLength(50)] string? ApplicationStatusCode,
    [property: StringLength(50)] string? SignatureStatusCode,
    [property: StringLength(50)] string? FundingStatusCode,
    [property: StringLength(50)] string? AccountStatusCode,
    [property: StringLength(50)] string? StatusCode,
    Guid? DocumentId,
    Guid? ESignEnvelopeId,
    DateOnly? FundedDate,
    DateOnly? NextPaymentDate,
    DateTime? ApprovedDateUtc,
    DateTime? ActivatedDateUtc,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? PayoffAmount,
    DateOnly? PayoffGoodThroughDate,
    [property: StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId,
    [property: StringLength(200)] string? ModifiedByName);

public sealed record PremiumFinanceScheduleItemRequest(
    [property: Range(1, 120)] int InstallmentNumber,
    DateOnly DueDate,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal ScheduledAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? PrincipalAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? FinanceChargeAmount,
    [property: Range(typeof(decimal), "0", "9999999999999999")] decimal? PaidAmount,
    DateOnly? PaidDate,
    [property: Required, StringLength(50)] string StatusCode,
    [property: StringLength(160)] string? ProviderPaymentReference);

public sealed record ReplacePremiumFinancePaymentScheduleRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid FinanceAgreementId,
    [property: Required, MinLength(1)] IReadOnlyList<PremiumFinanceScheduleItemRequest> Items,
    Guid? ModifiedByUserId,
    [property: StringLength(200)] string? ModifiedByName);

public sealed record AddPremiumFinanceActivityRequest(
    [property: Required] Guid TenantId,
    Guid? PremiumFinanceRequestId,
    Guid? FinanceAgreementId,
    [property: Required, StringLength(80)] string ActivityTypeCode,
    [property: Required, StringLength(200)] string Subject,
    [property: StringLength(2000)] string? Notes,
    [property: StringLength(160)] string? ProviderReference,
    Guid? CreatedByUserId,
    [property: StringLength(200)] string? CreatedByName);

public sealed record LinkPremiumFinanceDocumentRequest(
    [property: Required] Guid TenantId,
    Guid? PremiumFinanceRequestId,
    Guid? FinanceAgreementId,
    [property: Required] Guid DocumentId,
    [property: Required, StringLength(80)] string DocumentRoleCode,
    Guid? CreatedByUserId);

public sealed record UpsertPremiumFinanceProviderRequest(
    [property: Required] Guid TenantId,
    Guid? FinanceCompanyId,
    [property: Required, StringLength(50)] string CompanyCode,
    [property: Required, StringLength(200)] string CompanyName,
    [property: StringLength(160)] string? ContactName,
    [property: EmailAddress, StringLength(254)] string? EmailAddress,
    [property: Phone, StringLength(50)] string? PhoneNumber,
    [property: StringLength(1000)] string? RemittanceInstructions,
    [property: StringLength(100)] string? ProviderKey,
    [property: Required, StringLength(50)] string IntegrationLevelCode,
    [property: Url, StringLength(500)] string? WebsiteUrl,
    [property: Url, StringLength(500)] string? PortalUrl,
    bool SupportsQuotes,
    bool SupportsApplications,
    bool SupportsAgreements,
    bool SupportsPaymentSchedules,
    bool SupportsAccountStatus,
    bool SupportsPayoff,
    [property: StringLength(160)] string? ExternalProviderId,
    bool IsActive,
    Guid? UserId);

public sealed record CancelPremiumFinanceRequest(
    [property: Required] Guid TenantId,
    [property: Required] Guid PremiumFinanceRequestId,
    [property: Required, StringLength(1000)] string Reason,
    Guid? CancelledByUserId,
    [property: StringLength(200)] string? CancelledByName);
