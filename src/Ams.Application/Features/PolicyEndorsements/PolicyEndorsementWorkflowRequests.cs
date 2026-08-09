using Ams.Application.Common.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Ams.Application.Features.PolicyEndorsements;

public sealed class CreatePolicyEndorsementTransactionRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid PolicyId { get; set; }
    [Required, StringLength(50)]
    public string EndorsementTypeCode { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string ReasonCode { get; set; } = string.Empty;
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string PriorityCode { get; set; } = "Normal";
    [StringLength(50)]
    public string? CarrierMethodCode { get; set; }
    [StringLength(2000)]
    public string? InternalNotes { get; set; }
    [StringLength(2000)]
    public string? ClientFacingNotes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public PolicyEndorsementFinancialImpactInput FinancialImpact { get; set; } = new();
    [MinLength(1)]
    public List<PolicyEndorsementChangeInput> Changes { get; set; } = [];
    public Guid? CreatedByUserId { get; set; }
    [JsonIgnore]
    public bool AllowBackdate { get; set; }
    [JsonIgnore]
    public Guid? ReversalOfEndorsementId { get; set; }
    [JsonIgnore]
    public byte[]? ReversalOfRowVersion { get; set; }
}

public sealed class SavePolicyEndorsementDraftRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(50)]
    public string EndorsementTypeCode { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string ReasonCode { get; set; } = string.Empty;
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string PriorityCode { get; set; } = "Normal";
    [StringLength(50)]
    public string? CarrierMethodCode { get; set; }
    [StringLength(2000)]
    public string? InternalNotes { get; set; }
    [StringLength(2000)]
    public string? ClientFacingNotes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public PolicyEndorsementFinancialImpactInput FinancialImpact { get; set; } = new();
    [MinLength(1)]
    public List<PolicyEndorsementChangeInput> Changes { get; set; } = [];
    [Required, MinLength(1)]
    public byte[] RowVersion { get; set; } = [];
    public Guid? ModifiedByUserId { get; set; }
    [JsonIgnore]
    public bool AllowBackdate { get; set; }
}

public sealed class PolicyEndorsementFinancialImpactInput
{
    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "USD";
    [Range(-100000000, 100000000)]
    public decimal PremiumChange { get; set; }
    [Range(-100000000, 100000000)]
    public decimal AgencyFee { get; set; }
    [Range(-100000000, 100000000)]
    public decimal Taxes { get; set; }
    [Range(-100000000, 100000000)]
    public decimal ProratedPremiumChange { get; set; }
    [StringLength(50)]
    public string? BillingImpactCode { get; set; }
    [StringLength(50)]
    public string? CommissionImpactCode { get; set; }
}

public sealed class PolicyEndorsementChangeInput : IValidatableObject
{
    public Guid? ChangeId { get; set; }
    [Required, StringLength(50)]
    public string CategoryCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string OperationCode { get; set; } = string.Empty;
    [StringLength(200)]
    public string? EntityKey { get; set; }
    [StringLength(500)]
    public string? Summary { get; set; }
    public PolicyEndorsementInsuredChangeDto? Insured { get; set; }
    public PolicyEndorsementVehicleChangeDto? Vehicle { get; set; }
    public PolicyEndorsementDriverChangeDto? Driver { get; set; }
    public PolicyEndorsementCoverageChangeDto? Coverage { get; set; }
    public PolicyEndorsementPropertyChangeDto? Property { get; set; }
    public PolicyEndorsementCommercialChangeDto? Commercial { get; set; }
    public PolicyEndorsementFinancialChangeDto? Financial { get; set; }
    public PolicyEndorsementLegalChangeDto? Legal { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var typedValues = new object?[] { Insured, Vehicle, Driver, Coverage, Property, Commercial, Financial, Legal };
        if (typedValues.Count(value => value is not null) != 1)
            yield return new ValidationResult("Exactly one typed endorsement change is required.", [nameof(Insured), nameof(Vehicle), nameof(Driver), nameof(Coverage), nameof(Property), nameof(Commercial), nameof(Financial), nameof(Legal)]);

        var expectedCategory = Insured is not null ? "Insured"
            : Vehicle is not null ? "Vehicle"
            : Driver is not null ? "Driver"
            : Coverage is not null ? "Coverage"
            : Property is not null ? "Property"
            : Commercial is not null ? "Commercial"
            : Financial is not null ? "Financial"
            : Legal is not null ? "Legal"
            : null;
        if (expectedCategory is not null && !string.Equals(CategoryCode, expectedCategory, StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult($"CategoryCode must be '{expectedCategory}' for the supplied typed change.", [nameof(CategoryCode)]);
    }
}

public sealed class TransitionPolicyEndorsementRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(80)]
    public string ToStatusCode { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    [Required, MinLength(1)]
    public byte[] RowVersion { get; set; } = [];
    public Guid? ActorUserId { get; set; }
    [JsonIgnore]
    public IReadOnlyCollection<string> GrantedPermissions { get; set; } = [];
}

public sealed class AssignPolicyEndorsementApprovalRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid AssignedToUserId { get; set; }
    [Required, MinLength(1)]
    public byte[] ApprovalRowVersion { get; set; } = [];
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
}

public sealed class RequestPolicyEndorsementInformationRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)]
    public string RequestDetails { get; set; } = string.Empty;
    public DateTime? DueDateUtc { get; set; }
    [Required, MinLength(1)]
    public byte[] EndorsementRowVersion { get; set; } = [];
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
}

public sealed class RespondPolicyEndorsementInformationRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)]
    public string ResponseDetails { get; set; } = string.Empty;
    [Required, MinLength(1)]
    public byte[] EndorsementRowVersion { get; set; } = [];
    [Required, MinLength(1)]
    public byte[] InformationRequestRowVersion { get; set; } = [];
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
}

public sealed class ResubmitPolicyEndorsementInformationRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    [Required, MinLength(1)]
    public byte[] EndorsementRowVersion { get; set; } = [];
    [Required, MinLength(1)]
    public byte[] InformationRequestRowVersion { get; set; } = [];
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
}

public sealed class DecidePolicyEndorsementApprovalRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(20)]
    public string DecisionCode { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    [Required, MinLength(1)]
    public byte[] EndorsementRowVersion { get; set; } = [];
    [Required, MinLength(1)]
    public byte[] ApprovalRowVersion { get; set; } = [];
    [Required]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    [JsonIgnore]
    public IReadOnlyCollection<string> GrantedPermissions { get; set; } = [];
}

public sealed class QueuePolicyEndorsementCarrierSubmissionRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(50)]
    public string ChannelCode { get; set; } = string.Empty;
    public Guid? CarrierConfigurationId { get; set; }
    [StringLength(500)]
    public string? Recipient { get; set; }
    [StringLength(2000)]
    public string? SubmissionNotes { get; set; }
    public Guid? ActorUserId { get; set; }
    [JsonIgnore]
    public IReadOnlyCollection<string> GrantedPermissions { get; set; } = [];
}

public sealed class ReversePolicyEndorsementRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
    [Required, MinLength(1)]
    public byte[] RowVersion { get; set; } = [];
    public Guid? ActorUserId { get; set; }
    [JsonIgnore]
    public IReadOnlyCollection<string> GrantedPermissions { get; set; } = [];
    [JsonIgnore]
    public bool AllowBackdate { get; set; }
}

public sealed record CompletePolicyEndorsementCarrierDispatch(
    string StatusCode,
    string? ExternalReferenceNumber,
    string? ResponsePayload,
    int? HttpStatusCode);

public sealed record FailPolicyEndorsementCarrierDispatch(
    string ErrorCode,
    string ErrorMessage,
    bool IsRetryable,
    DateTime? RetryAtUtc = null,
    string? ResponsePayload = null,
    int? HttpStatusCode = null);

public sealed record CompletePolicyEndorsementAccountingWork(
    string ResultEntityName,
    Guid ResultEntityId);

public sealed record FailPolicyEndorsementWork(
    string ErrorMessage,
    bool IsRetryable,
    DateTime? RetryAtUtc = null);

public sealed record CompletePolicyEndorsementDocumentWork(Guid DocumentId);

public sealed class PolicyEndorsementAccountingWorkItem
{
    public Guid AccountingWorkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid PolicyId { get; set; }
    public string WorkTypeCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal PremiumAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
}

public sealed class PolicyEndorsementDocumentWorkItem
{
    public Guid DocumentWorkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid PolicyId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
}
