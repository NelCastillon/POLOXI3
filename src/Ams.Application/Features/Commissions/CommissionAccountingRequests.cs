using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Commissions;

public sealed record ImportCarrierCommissionStatementRequest(
    [Required] Guid TenantId,
    Guid? CarrierId,
    [Required, StringLength(100)] string StatementNumber,
    DateOnly StatementDate,
    DateOnly? PeriodStartDate,
    DateOnly? PeriodEndDate,
    [StringLength(50)] string? BillingTypeCode,
    [Required, StringLength(3)] string CurrencyCode,
    [Required, StringLength(260)] string SourceFileName,
    [Required] string CsvContent,
    Guid? ImportProfileId,
    Guid? ImportedByUserId);

public sealed record RunCommissionMatchingRequest(
    [Required] Guid TenantId,
    [Required] Guid CarrierCommissionStatementId,
    [Range(0, 1000000)] decimal AmountTolerance,
    [Range(0, 365)] int DateToleranceDays,
    Guid? UserId);

public sealed record ProposedCommissionMatch(
    Guid CarrierCommissionStatementLineId,
    Guid CommissionExpectedReceivableId,
    decimal MatchScore,
    string MatchMethodCode,
    decimal MatchedAmount,
    decimal ExpectedAmount);

public sealed record ApproveCommissionMatchRequest(
    [Required] Guid TenantId,
    [Required] Guid CommissionReconciliationMatchId,
    [StringLength(1000)] string? Notes,
    Guid? ApprovedByUserId);

public sealed record ResolveCommissionReconciliationExceptionRequest(
    [Required] Guid TenantId,
    [Required] Guid CommissionReconciliationExceptionId,
    [Required, StringLength(2000)] string ResolutionNotes,
    Guid? ResolvedByUserId);

public sealed record CreateCommissionPayableBatchRequest(
    [Required] Guid TenantId,
    DateOnly AccountingThroughDate,
    Guid? PayeeId,
    [Required, StringLength(3)] string CurrencyCode,
    Guid? CreatedByUserId);

public sealed record ApproveCommissionPayableRequest(
    [Required] Guid TenantId,
    [Required] Guid CommissionPayableId,
    Guid? ApprovedByUserId);

public sealed record SynchronizeCommissionExpectedReceivablesRequest(
    [Required] Guid TenantId,
    DateOnly? EffectiveFromDate,
    DateOnly? EffectiveThroughDate,
    Guid? UserId);
