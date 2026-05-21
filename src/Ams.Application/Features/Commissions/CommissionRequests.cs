using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.Finance;

namespace Ams.Application.Features.Commissions;

public class CreateCommissionPlanRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string PlanCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string PlanName { get; set; } = string.Empty;
    [StringLength(50)] public string PlanTypeCode { get; set; } = "Standard";
    [Range(0, 100)] public decimal NewBusinessRatePct { get; set; }
    [Range(0, 100)] public decimal RenewalRatePct { get; set; }
    [Required] public DateOnly EffectiveStartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string StatusCode { get; set; } = "Draft";
    public bool AllowSplit { get; set; }
    public bool HouseAccountRules { get; set; }
    public bool BranchOverrideEligible { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionPlanRequest : CreateCommissionPlanRequest
{
    [Required] public Guid CommissionPlanId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionSplitRuleRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Commission Plan is required.")] public Guid CommissionPlanId { get; set; }
    [Required, StringLength(200)] public string RuleName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string SplitTypeCode { get; set; } = "Producer";
    public Guid? PayeeId { get; set; }
    [Range(0, 100)] public decimal SplitPct { get; set; }
    [Range(0, 100)] public decimal? OverrideRatePct { get; set; }
    [Range(0, 9999)] public int Priority { get; set; } = 100;
    [Required] public DateOnly EffectiveStartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EffectiveEndDate { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Active";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionSplitRuleRequest : CreateCommissionSplitRuleRequest
{
    [Required] public Guid SplitRuleId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionClawbackRequest
{
    [Required] public Guid TenantId { get; set; }
    public Guid? PayeeId { get; set; }
    public Guid? OriginalTransactionId { get; set; }
    [Required] public DateOnly ClawbackDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal Amount { get; set; }
    [Required, StringLength(100)] public string ReasonCode { get; set; } = "Policy Cancellation";
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionClawbackRequest : CreateCommissionClawbackRequest
{
    [Required] public Guid ClawbackId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionPayoutBatchRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(80)] public string BatchReference { get; set; } = string.Empty;
    [Required] public DateOnly PayPeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-14));
    [Required] public DateOnly PayPeriodEnd { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    [Range(0, 100000000)] public decimal TotalAmount { get; set; }
    [Range(0, 1000000)] public int PayoutCount { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Draft";
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionPayoutBatchRequest : CreateCommissionPayoutBatchRequest
{
    [Required] public Guid PayoutBatchId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionDisputeRequest
{
    [Required] public Guid TenantId { get; set; }
    public Guid? PayeeId { get; set; }
    public Guid? TransactionId { get; set; }
    [Required] public DateOnly DisputeDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required, StringLength(500)] public string DisputeReason { get; set; } = string.Empty;
    [Range(0.01, 100000000)] public decimal DisputedAmount { get; set; }
    [StringLength(1000)] public string? Resolution { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Open";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionDisputeRequest : CreateCommissionDisputeRequest
{
    [Required] public Guid DisputeId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionPayeeRequest
{
    [Required] public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    [RequiredGuid(ErrorMessage = "Commission Plan is required. ")] public Guid CommissionPlanId { get; set; }
    [Required, StringLength(50)] public string PayeeTypeCode { get; set; } = "Producer";
    [Range(0, 100)] public decimal SplitPercentage { get; set; } = 100;
    [Required] public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Active";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionPayeeRequest : CreateCommissionPayeeRequest
{
    [Required] public Guid PayeeId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionTransactionRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Payee is required.")] public Guid PayeeId { get; set; }
    [RequiredGuid(ErrorMessage = "Commission Plan is required.")] public Guid CommissionPlanId { get; set; }
    [Required, StringLength(100)] public string SourceEntityName { get; set; } = "Policy";
    [Required] public Guid SourceEntityId { get; set; } = Guid.NewGuid();
    [Required] public DateOnly TransactionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal GrossAmount { get; set; }
    [Range(0, 100)] public decimal CommissionRate { get; set; }
    [Range(0, 100000000)] public decimal CommissionAmount { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? PayoutId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionTransactionRequest : CreateCommissionTransactionRequest
{
    [Required] public Guid TransactionId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionPayoutRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Payee is required.")] public Guid PayeeId { get; set; }
    [Required] public DateOnly PayoutDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal TotalAmount { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Draft";
    public DateTime? ProcessedDateUtc { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionPayoutRequest : CreateCommissionPayoutRequest
{
    [Required] public Guid PayoutId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCommissionPayoutStatementRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Payee is required.")] public Guid PayeeId { get; set; }
    public Guid? PayoutBatchId { get; set; }
    [Required] public DateOnly StatementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0, 100000000)] public decimal GrossEarnings { get; set; }
    [Range(0, 100000000)] public decimal TotalClawbacks { get; set; }
    [Range(0, 100000000)] public decimal NetPayout { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = "USD";
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Draft";
    public DateTime? IssuedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCommissionPayoutStatementRequest : CreateCommissionPayoutStatementRequest
{
    [Required] public Guid StatementId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class GenerateCommissionPayoutStatementsRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required] public DateOnly PayPeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-14));
    [Required] public DateOnly PayPeriodEnd { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    public Guid? PayeeId { get; set; }
    [Range(0, 100)] public decimal ClawbackPercent { get; set; } = 0;
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Draft";
    public bool IssueImmediately { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
