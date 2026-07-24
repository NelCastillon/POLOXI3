using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed record SynchronizeAgencyBillRequest([Required] Guid TenantId, DateOnly? FromDate, DateOnly? ThroughDate, Guid? UserId);

public sealed record CreateAgencyBillInstallmentScheduleRequest([Required] Guid TenantId, [Required] Guid AgencyBillReceivableId, [Range(1, 120)] int InstallmentCount, DateOnly FirstDueDate, [Range(1, 24)] int FrequencyMonths, [Range(0, 90)] int GraceDays, Guid? UserId);

public sealed record AllocateAgencyBillPaymentRequest([Required] Guid TenantId, [Required] Guid PaymentId, [Required] Guid AgencyBillReceivableId, Guid? AgencyBillInstallmentId, [Range(typeof(decimal), "0.01", "9999999999999999")] decimal AllocationAmount, [StringLength(1000)] string? Notes, Guid? UserId);

public sealed record ReverseAgencyBillPaymentAllocationRequest([Required] Guid TenantId, [Required] Guid AgencyBillPaymentAllocationId, [Required, StringLength(1000)] string Reason, Guid? UserId);

public sealed record RunAgencyBillDelinquencyRequest([Required] Guid TenantId, DateOnly AsOfDate, Guid? UserId);

public sealed record CreateAgencyBillLateNoticeRequest([Required] Guid TenantId, [Required] Guid AgencyBillReceivableId, Guid? AgencyBillInstallmentId, [Required, StringLength(40)] string NoticeLevelCode, [Required, StringLength(40)] string DeliveryMethodCode, [EmailAddress, StringLength(254)] string? RecipientAddress, Guid? UserId);

public sealed record CreateNonPaymentReferralRequest([Required] Guid TenantId, [Required] Guid AgencyBillReceivableId, DateOnly? RequestedEffectiveDate, [StringLength(2000)] string? Notes, Guid? UserId);

public sealed record ReviewNonPaymentReferralRequest([Required] Guid TenantId, [Required] Guid NonPaymentCancellationReferralId, [Required, StringLength(50)] string DecisionCode, [Required, StringLength(2000)] string ReviewNotes, Guid? UserId);

public sealed record CreateAgencyBillReconciliationRequest([Required] Guid TenantId, [Required, StringLength(100)] string StatementReference, DateOnly StatementDate, DateOnly? PeriodStartDate, DateOnly? PeriodEndDate, [Range(typeof(decimal), "0", "9999999999999999")] decimal StatementAmount, Guid? UserId);

public sealed record AddAgencyBillReconciliationLineRequest([Required] Guid TenantId, [Required] Guid AgencyBillReconciliationId, Guid? AgencyBillReceivableId, Guid? PaymentAllocationId, [StringLength(100)] string? StatementLineReference, decimal StatementAmount, decimal SubledgerAmount, [StringLength(1000)] string? ExceptionReason, Guid? UserId);

public sealed record CompleteAgencyBillReconciliationRequest([Required] Guid TenantId, [Required] Guid AgencyBillReconciliationId, [Range(typeof(decimal), "0", "9999999999999999")] decimal VarianceTolerance, Guid? UserId);

public sealed record UpsertFinanceCompanyRequest([Required] Guid TenantId, Guid? FinanceCompanyId, [Required, StringLength(50)] string CompanyCode, [Required, StringLength(200)] string CompanyName, [StringLength(160)] string? ContactName, [EmailAddress, StringLength(254)] string? EmailAddress, [Phone, StringLength(50)] string? PhoneNumber, [StringLength(1000)] string? RemittanceInstructions, bool IsActive, Guid? UserId);

public sealed record CreateFinanceAgreementRequest([Required] Guid TenantId, [Required] Guid AgencyBillReceivableId, [Required] Guid FinanceCompanyId, [Required, StringLength(100)] string AgreementNumber, [Range(typeof(decimal), "0.01", "9999999999999999")] decimal FinancedAmount, [Range(typeof(decimal), "0", "9999999999999999")] decimal DownPaymentAmount, DateOnly? ExpectedFundingDate, DateOnly? CancellationProtectionDate, Guid? UserId);

public sealed record UpdateFinanceAgreementFundingRequest([Required] Guid TenantId, [Required] Guid FinanceAgreementId, [Required, StringLength(50)] string FundingStatusCode, DateOnly? FundedDate, [StringLength(1000)] string? Notes, Guid? UserId);
