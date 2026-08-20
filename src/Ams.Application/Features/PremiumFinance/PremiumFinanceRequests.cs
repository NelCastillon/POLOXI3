using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PremiumFinance;

public sealed record CreatePremiumFinanceRequest(
    [Required] Guid TenantId,
    [Required, StringLength(40)] string SourceTypeCode,
    Guid? QuoteId,
    Guid? PolicyId,
    Guid? RenewalId,
    Guid? SubmissionId,
    [Required] Guid AccountId,
    Guid? CarrierId,
    Guid? ProducerUserId,
    Guid? AssignedToUserId,
    [Required, StringLength(200)] string InsuredName,
    [StringLength(200)] string? AgencyName,
    [StringLength(200)] string? ProducerName,
    [StringLength(200)] string? CarrierName,
    [Required, StringLength(120)] string PolicyOrQuoteNumber,
    [Required, StringLength(160)] string LineOfBusiness,
    DateOnly EffectiveDate,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal PremiumAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal TaxAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal FeeAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? RequestedDownPaymentAmount,
    [Range(1, 120)] int? RequestedInstallmentCount,
    Guid? PreferredFinanceCompanyId,
    [EmailAddress, StringLength(254)] string? CustomerEmail,
    [Phone, StringLength(50)] string? CustomerPhone,
    [StringLength(2000)] string? Notes,
    Guid? CreatedByUserId,
    [StringLength(200)] string? CreatedByName);

public sealed record UpdatePremiumFinanceRequest(
    [Required] Guid TenantId,
    Guid? AssignedToUserId,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? RequestedDownPaymentAmount,
    [Range(1, 120)] int? RequestedInstallmentCount,
    Guid? PreferredFinanceCompanyId,
    [EmailAddress, StringLength(254)] string? CustomerEmail,
    [Phone, StringLength(50)] string? CustomerPhone,
    [StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId);

public sealed record UpdatePremiumFinanceStatusRequest(
    [Required] Guid TenantId,
    [Required, StringLength(50)] string StatusCode,
    [StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId,
    [StringLength(200)] string? ModifiedByName);

public sealed record AddPremiumFinanceQuoteOptionRequest(
    [Required] Guid TenantId,
    [Required] Guid PremiumFinanceRequestId,
    [Required] Guid FinanceCompanyId,
    [StringLength(160)] string? ProviderQuoteReference,
    [Required, StringLength(160)] string OptionName,
    [Range(typeof(decimal), "0", "100")] decimal DownPaymentPercent,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal DownPaymentAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal AmountFinanced,
    [Range(typeof(decimal), "0", "100")] decimal AprPercent,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal FinanceChargeAmount,
    [Range(1, 120)] int PaymentCount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal PaymentAmount,
    DateOnly? FirstPaymentDate,
    DateOnly? QuoteExpirationDate,
    [StringLength(2000)] string? TermsSummary,
    Guid? CreatedByUserId,
    [StringLength(200)] string? CreatedByName);

public sealed record SelectPremiumFinanceQuoteOptionRequest(
    [Required] Guid TenantId,
    [Required] Guid PremiumFinanceQuoteOptionId,
    Guid? SelectedByUserId,
    [StringLength(200)] string? SelectedByName);

public sealed record SubmitPremiumFinanceApplicationRequest(
    [Required] Guid TenantId,
    [Required] Guid PremiumFinanceRequestId,
    [Required] Guid FinanceCompanyId,
    [Required, StringLength(100)] string AgreementNumber,
    [StringLength(160)] string? ProviderApplicationReference,
    DateOnly? ExpectedFundingDate,
    DateOnly? CancellationProtectionDate,
    Guid? SubmittedByUserId,
    [StringLength(200)] string? SubmittedByName);

public sealed record UpdatePremiumFinanceAgreementRequest(
    [Required] Guid TenantId,
    [Required] Guid FinanceAgreementId,
    [StringLength(50)] string? ApplicationStatusCode,
    [StringLength(50)] string? SignatureStatusCode,
    [StringLength(50)] string? FundingStatusCode,
    [StringLength(50)] string? AccountStatusCode,
    [StringLength(50)] string? StatusCode,
    Guid? DocumentId,
    Guid? ESignEnvelopeId,
    DateOnly? FundedDate,
    DateOnly? NextPaymentDate,
    DateTime? ApprovedDateUtc,
    DateTime? ActivatedDateUtc,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? PayoffAmount,
    DateOnly? PayoffGoodThroughDate,
    [StringLength(2000)] string? Notes,
    Guid? ModifiedByUserId,
    [StringLength(200)] string? ModifiedByName);

public sealed record PremiumFinanceScheduleItemRequest(
    [Range(1, 120)] int InstallmentNumber,
    DateOnly DueDate,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal ScheduledAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? PrincipalAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? FinanceChargeAmount,
    [Range(typeof(decimal), "0", "9999999999999999")] decimal? PaidAmount,
    DateOnly? PaidDate,
    [Required, StringLength(50)] string StatusCode,
    [StringLength(160)] string? ProviderPaymentReference);

public sealed record ReplacePremiumFinancePaymentScheduleRequest(
    [Required] Guid TenantId,
    [Required] Guid FinanceAgreementId,
    [Required, MinLength(1)] IReadOnlyList<PremiumFinanceScheduleItemRequest> Items,
    Guid? ModifiedByUserId,
    [StringLength(200)] string? ModifiedByName);

public sealed record AddPremiumFinanceActivityRequest(
    [Required] Guid TenantId,
    Guid? PremiumFinanceRequestId,
    Guid? FinanceAgreementId,
    [Required, StringLength(80)] string ActivityTypeCode,
    [Required, StringLength(200)] string Subject,
    [StringLength(2000)] string? Notes,
    [StringLength(160)] string? ProviderReference,
    Guid? CreatedByUserId,
    [StringLength(200)] string? CreatedByName);

public sealed record LinkPremiumFinanceDocumentRequest(
    [Required] Guid TenantId,
    Guid? PremiumFinanceRequestId,
    Guid? FinanceAgreementId,
    [Required] Guid DocumentId,
    [Required, StringLength(80)] string DocumentRoleCode,
    Guid? CreatedByUserId);

public sealed record UpsertPremiumFinanceProviderRequest(
    [Required] Guid TenantId,
    Guid? FinanceCompanyId,
    [Required, StringLength(50)] string CompanyCode,
    [Required, StringLength(200)] string CompanyName,
    [StringLength(160)] string? ContactName,
    [EmailAddress, StringLength(254)] string? EmailAddress,
    [Phone, StringLength(50)] string? PhoneNumber,
    [StringLength(1000)] string? RemittanceInstructions,
    [StringLength(100)] string? ProviderKey,
    [Required, StringLength(50)] string IntegrationLevelCode,
    [Url, StringLength(500)] string? WebsiteUrl,
    [Url, StringLength(500)] string? PortalUrl,
    bool SupportsQuotes,
    bool SupportsApplications,
    bool SupportsAgreements,
    bool SupportsPaymentSchedules,
    bool SupportsAccountStatus,
    bool SupportsPayoff,
    [StringLength(160)] string? ExternalProviderId,
    bool IsActive,
    Guid? UserId);

public sealed record CancelPremiumFinanceRequest(
    [Required] Guid TenantId,
    [Required] Guid PremiumFinanceRequestId,
    [Required, StringLength(1000)] string Reason,
    Guid? CancelledByUserId,
    [StringLength(200)] string? CancelledByName);
