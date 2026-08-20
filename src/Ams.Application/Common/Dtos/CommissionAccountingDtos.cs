namespace Ams.Application.Common.Dtos;

public sealed record CommissionAccountingOptionDto(Guid CommissionAccountingOptionId, Guid TenantId, string OptionGroupCode, string OptionCode, string DisplayName, string? Description, bool IsDefault, bool IsActive, int SortOrder);

public sealed record CommissionExpectedReceivableDto(Guid CommissionExpectedReceivableId, Guid TenantId, Guid? SourceLedgerId, Guid? PolicyId, Guid? AccountId, Guid? CarrierId, string PolicyNumber, string? AccountName, string? CarrierName, string? LineOfBusinessCode, string BusinessTypeCode, string BillingTypeCode, string TransactionTypeCode, DateOnly? EffectiveDate, DateOnly StatementPeriodStart, DateOnly StatementPeriodEnd, decimal PremiumAmount, decimal ExpectedRatePct, decimal ExpectedCommissionAmount, decimal ReceivedCommissionAmount, decimal ReconciledCommissionAmount, string CurrencyCode, string StatusCode, DateOnly? DueDate);

public sealed record CarrierCommissionStatementDto(Guid CarrierCommissionStatementId, Guid TenantId, Guid? CarrierId, string StatementNumber, DateOnly StatementDate, DateOnly? PeriodStartDate, DateOnly? PeriodEndDate, string? BillingTypeCode, string CurrencyCode, decimal GrossPremiumAmount, decimal CommissionAmount, decimal ChargebackAmount, decimal NetReceivedAmount, string? SourceFileName, string ImportStatusCode, string ReconciliationStatusCode, DateTime ImportedDateUtc, int LineCount, int MatchedLineCount, int ExceptionCount);

public sealed record CarrierCommissionStatementLineDto(Guid CarrierCommissionStatementLineId, Guid TenantId, Guid CarrierCommissionStatementId, int LineNumber, string? ExternalTransactionId, string? PolicyNumber, string? InsuredName, string? ProducerCode, string? LineOfBusinessCode, string TransactionTypeCode, string? BillingTypeCode, DateOnly? TransactionDate, DateOnly? EffectiveDate, decimal PremiumAmount, decimal? CommissionRatePct, decimal CommissionAmount, decimal ChargebackAmount, decimal NetAmount, string CurrencyCode, string MatchStatusCode, string? ValidationErrorsJson);

public sealed record CommissionReconciliationMatchDto(Guid CommissionReconciliationMatchId, Guid TenantId, Guid CarrierCommissionStatementLineId, Guid CommissionExpectedReceivableId, string MatchMethodCode, decimal? MatchScore, decimal MatchedAmount, decimal VarianceAmount, string StatusCode, DateTime MatchedDateUtc, DateTime? ApprovedDateUtc, string? Notes);

public sealed record CommissionReconciliationExceptionDto(Guid CommissionReconciliationExceptionId, Guid TenantId, Guid? CarrierCommissionStatementId, Guid? CarrierCommissionStatementLineId, Guid? CommissionExpectedReceivableId, string ExceptionNumber, string ExceptionTypeCode, string SeverityCode, string StatusCode, decimal? ExpectedAmount, decimal? ReceivedAmount, decimal? VarianceAmount, string Description, string? ResolutionNotes, Guid? AssignedToUserId, DateTime? ResolvedDateUtc, DateTime CreatedDateUtc);

public sealed record CommissionPayableDto(Guid CommissionPayableId, Guid TenantId, Guid PayeeId, string? PayeeName, Guid? CommissionReconciliationMatchId, Guid? CommissionTransactionId, Guid? ClawbackId, Guid? PayoutBatchId, string PayableNumber, string PayableTypeCode, DateOnly AccountingDate, decimal GrossPayableAmount, decimal AdjustmentAmount, decimal NetPayableAmount, string CurrencyCode, string StatusCode, DateTime? ApprovedDateUtc, DateTime? PaidDateUtc);

public sealed record CommissionReconciliationSummaryDto(decimal TotalExpected, decimal TotalReceived, decimal TotalReconciled, decimal OpenVariance, int UnmatchedLineCount, int OpenExceptionCount, decimal ApprovedPayables, decimal PendingPayables);

public sealed record CommissionAccountingWorkspaceDto(IReadOnlyList<CommissionAccountingOptionDto> Options, IReadOnlyList<CommissionExpectedReceivableDto> ExpectedReceivables, IReadOnlyList<CarrierCommissionStatementDto> Statements, IReadOnlyList<CommissionReconciliationMatchDto> Matches, IReadOnlyList<CommissionReconciliationExceptionDto> Exceptions, IReadOnlyList<CommissionPayableDto> Payables, CommissionReconciliationSummaryDto Summary);

public sealed record CommissionImportResultDto(Guid CarrierCommissionStatementId, int ImportedLineCount, int MatchedLineCount, int ExceptionCount, string StatusCode);

public sealed record CommissionMatchRunResultDto(Guid CarrierCommissionStatementId, int ExactMatches, int ToleranceMatches, int UnmatchedLines, int ExceptionsCreated);
