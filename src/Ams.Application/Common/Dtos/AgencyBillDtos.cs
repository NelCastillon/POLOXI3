namespace Ams.Application.Common.Dtos;

public sealed record AgencyBillOptionDto(Guid AgencyBillOptionId, Guid TenantId, string OptionGroupCode, string OptionCode, string DisplayName, string? Description, decimal? NumericValue, bool IsDefault, bool IsActive, int SortOrder);

public sealed record AgencyBillReceivableDto(Guid AgencyBillReceivableId, Guid TenantId, string ReceivableNumber, string SourceTypeCode, Guid? SourceInvoiceId, Guid? PolicyId, Guid? PolicyTermId, Guid AccountId, Guid? CarrierId, string BillingTypeCode, string CurrencyCode, DateOnly TransactionDate, DateOnly DueDate, decimal OriginalAmount, decimal AllocatedAmount, decimal AdjustedAmount, decimal BalanceAmount, string StatusCode, string DelinquencyStageCode, Guid? FinanceCompanyId, string? Notes, DateTime CreatedDateUtc);

public sealed record AgencyBillInstallmentDto(Guid AgencyBillInstallmentId, Guid TenantId, Guid AgencyBillReceivableId, int InstallmentNumber, DateOnly DueDate, decimal InstallmentAmount, decimal AllocatedAmount, decimal BalanceAmount, string StatusCode, DateOnly? GraceDate);

public sealed record AgencyBillPaymentAllocationDto(Guid AgencyBillPaymentAllocationId, Guid TenantId, Guid PaymentId, Guid AgencyBillReceivableId, Guid? AgencyBillInstallmentId, decimal AllocationAmount, DateTime AllocationDateUtc, string StatusCode, Guid? ReversalOfAllocationId, string? Notes);

public sealed record PremiumTrustTransactionDto(Guid PremiumTrustTransactionId, Guid TenantId, string TrustAccountCode, string TransactionTypeCode, Guid? AgencyBillReceivableId, Guid? PaymentId, Guid? PaymentAllocationId, DateOnly TransactionDate, decimal Amount, string DirectionCode, string? ReferenceNumber, string StatusCode, Guid? ReversalOfTransactionId);

public sealed record AgencyBillLateNoticeDto(Guid AgencyBillLateNoticeId, Guid TenantId, Guid AgencyBillReceivableId, Guid? AgencyBillInstallmentId, string NoticeLevelCode, DateTime NoticeDateUtc, string DeliveryMethodCode, string? RecipientAddress, decimal AmountDue, DateOnly DueDate, string StatusCode, string? DeliveryReference);

public sealed record NonPaymentCancellationReferralDto(Guid NonPaymentCancellationReferralId, Guid TenantId, string ReferralNumber, Guid AgencyBillReceivableId, Guid? PolicyId, Guid? PolicyCancellationId, DateTime ReferralDateUtc, DateOnly? RequestedEffectiveDate, decimal AmountDue, string StatusCode, string? ReviewNotes, DateTime? ReviewedDateUtc, Guid? ReviewedByUserId);

public sealed record AgencyBillReconciliationDto(Guid AgencyBillReconciliationId, Guid TenantId, string ReconciliationNumber, string StatementReference, DateOnly StatementDate, DateOnly? PeriodStartDate, DateOnly? PeriodEndDate, decimal StatementAmount, decimal SubledgerAmount, decimal VarianceAmount, string StatusCode, DateTime? CompletedDateUtc);

public sealed record AgencyBillReconciliationLineDto(Guid AgencyBillReconciliationLineId, Guid TenantId, Guid AgencyBillReconciliationId, Guid? AgencyBillReceivableId, Guid? PaymentAllocationId, string? StatementLineReference, decimal StatementAmount, decimal SubledgerAmount, decimal VarianceAmount, string MatchStatusCode, string? ExceptionReason);

public sealed record FinanceCompanyDto(Guid FinanceCompanyId, Guid TenantId, string CompanyCode, string CompanyName, string? ContactName, string? EmailAddress, string? PhoneNumber, string? RemittanceInstructions, bool IsActive);

public sealed record FinanceAgreementDto(Guid FinanceAgreementId, Guid TenantId, Guid AgencyBillReceivableId, Guid FinanceCompanyId, string AgreementNumber, decimal FinancedAmount, decimal DownPaymentAmount, string FundingStatusCode, DateOnly? ExpectedFundingDate, DateOnly? FundedDate, DateOnly? CancellationProtectionDate, string StatusCode);

public sealed record AgencyBillSummaryDto(decimal TotalReceivable, decimal TotalAllocated, decimal OpenBalance, decimal PremiumTrustBalance, decimal ReconciliationVariance, int PastDueCount, int OpenLateNoticeCount, int PendingReferralCount, int PendingFinanceAgreementCount);

public sealed record AgencyBillWorkspaceDto(IReadOnlyList<AgencyBillOptionDto> Options, IReadOnlyList<AgencyBillReceivableDto> Receivables, IReadOnlyList<AgencyBillInstallmentDto> Installments, IReadOnlyList<AgencyBillPaymentAllocationDto> Allocations, IReadOnlyList<PremiumTrustTransactionDto> TrustTransactions, IReadOnlyList<AgencyBillLateNoticeDto> LateNotices, IReadOnlyList<NonPaymentCancellationReferralDto> Referrals, IReadOnlyList<AgencyBillReconciliationDto> Reconciliations, IReadOnlyList<FinanceCompanyDto> FinanceCompanies, IReadOnlyList<FinanceAgreementDto> FinanceAgreements, AgencyBillSummaryDto Summary);

public sealed record AgencyBillSynchronizationResultDto(int ReceivablesCreated, int InstallmentsCreated, int AllocationsCreated, int TrustTransactionsCreated);
public sealed record AgencyBillDelinquencyRunResultDto(int ReceivablesEvaluated, int NoticesCreated, int ReferralsCreated);
