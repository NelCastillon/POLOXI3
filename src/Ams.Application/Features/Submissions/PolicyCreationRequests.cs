namespace Ams.Application.Features.Submissions;

using System.ComponentModel.DataAnnotations;

public sealed record PolicyCreationFromConfirmedBindRequest(
    Guid TenantId,
    Guid PolicyBindTransactionId,
    Guid? RequestedByUserId = null);

public sealed class UpsertBinderReviewRequest : IValidatableObject
{
    [Required] public Guid TenantId { get; set; }
    [StringLength(80)] public string? PolicyNumber { get; set; }
    [Required] public Guid CarrierId { get; set; }
    [Required, StringLength(160)] public string LineOfBusiness { get; set; } = string.Empty;
    [Required] public DateOnly EffectiveDate { get; set; }
    [Required] public DateOnly ExpirationDate { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal Premium { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal? Fees { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal? Taxes { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? CommissionPercent { get; set; }
    [StringLength(200)] public string? PaymentPlan { get; set; }
    [StringLength(50)] public string? BillingTypeCode { get; set; }
    public Guid? ProducerId { get; set; }
    public Guid? CsrId { get; set; }
    [Required] public string CoverageSnapshotJson { get; set; } = "{}";
    [Required] public string RiskSnapshotJson { get; set; } = "{}";
    [Required] public string ComparisonSnapshotJson { get; set; } = "{}";
    [StringLength(2000)] public string? ReviewNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpirationDate <= EffectiveDate)
            yield return new ValidationResult("Expiration date must be after the effective date.", [nameof(ExpirationDate)]);
        foreach (var value in new[] { (CoverageSnapshotJson, nameof(CoverageSnapshotJson)), (RiskSnapshotJson, nameof(RiskSnapshotJson)), (ComparisonSnapshotJson, nameof(ComparisonSnapshotJson)) })
        {
            var isValidJson = true;
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(value.Item1);
            }
            catch (System.Text.Json.JsonException)
            {
                isValidJson = false;
            }

            if (!isValidJson)
            {
                yield return new ValidationResult($"{value.Item2} must contain valid JSON.", [value.Item2]);
            }
        }
    }
}

public sealed record DecideBinderReviewRequest(
    [property: Required] Guid TenantId,
    [property: Required, RegularExpression("Accepted|Rejected|CorrectionRequested")] string DecisionCode,
    [property: StringLength(2000)] string? Notes,
    Guid? DecidedByUserId);

public sealed record QueuePolicyGenerationRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(120)] string IdempotencyKey,
    Guid? RequestedByUserId);

public sealed class ManualPolicyOptionDto
{
    public Guid OptionId { get; set; }
    public Guid TenantId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresDocument { get; set; }
    public bool RequiresElevatedPermission { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ManualPolicyDraftDto
{
    public Guid DraftId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public int CurrentStep { get; set; }
    public string StatusCode { get; set; } = "InProgress";
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class ManualPolicyDuplicateCandidateDto
{
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string NormalizedPolicyNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string Classification { get; set; } = "PossibleDuplicate";
}

public sealed class ManualPolicyValidationResultDto
{
    public List<string> BlockingErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ManualPolicyDuplicateCandidateDto> Duplicates { get; set; } = new();
    public bool CanCreate => BlockingErrors.Count == 0 && !Duplicates.Any(d => string.Equals(d.Classification, "ExactDuplicate", StringComparison.OrdinalIgnoreCase));
}

public sealed class ManualPolicyCreateResultDto
{
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingVerification";
    public string DataCompleteness { get; set; } = "Partial";
}

public sealed class ManualPolicyLineRequest
{
    public Guid? LineOfBusinessId { get; set; }
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public string LineOfBusinessName { get; set; } = string.Empty;
    public string PolicyLineStatusCode { get; set; } = "Active";
    public decimal? WrittenPremium { get; set; }
    public string? CoverageSummary { get; set; }
    public string? LimitsSummary { get; set; }
    public string? DeductibleSummary { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpsertManualPolicyDraftRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public int CurrentStep { get; set; } = 1;
    public string PayloadJson { get; set; } = "{}";
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class CreateManualPolicyRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? DraftId { get; set; }
    public Guid CarrierId { get; set; }
    public Guid? WritingCompanyId { get; set; }
    public Guid? BrokerOrMgaId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string? BinderNumber { get; set; }
    public string? QuoteNumber { get; set; }
    public string? CarrierCustomerNumber { get; set; }
    public string? ExternalReference { get; set; }
    public string PolicySourceCode { get; set; } = "ManualExistingPolicy";
    public string ManualReasonCode { get; set; } = string.Empty;
    public string? ExternalSystem { get; set; }
    public string? CarrierPortalReference { get; set; }
    public string? MigrationBatch { get; set; }
    public bool BrokerOfRecord { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public string PolicyStatus { get; set; } = "PendingVerification";
    public string TermStatus { get; set; } = "Active";
    public string TransactionTypeCode { get; set; } = "Conversion";
    public string? PolicyForm { get; set; }
    public string? PolicyDescription { get; set; }
    public string NamedInsured { get; set; } = string.Empty;
    public string? DbaName { get; set; }
    public string? MailingAddressJson { get; set; }
    public string? RiskSnapshotJson { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public DateOnly? PolicyIssueDate { get; set; }
    public DateOnly? RetroactiveDate { get; set; }
    public DateOnly? CancellationDate { get; set; }
    public decimal? WrittenPremium { get; set; }
    public decimal? AnnualizedPremium { get; set; }
    public decimal? Taxes { get; set; }
    public decimal? Fees { get; set; }
    public decimal? Surcharges { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? DownPayment { get; set; }
    public string BillingTypeCode { get; set; } = "DirectBill";
    public string? PaymentPlan { get; set; }
    public string? FinanceCompany { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public bool CreateAgencyInvoice { get; set; }
    public bool CreateInstallmentSchedule { get; set; }
    public bool TrackPaymentReceivable { get; set; }
    public string? CoverageSummary { get; set; }
    public string? LimitsSummary { get; set; }
    public string? DeductibleSummary { get; set; }
    public string? CoverageNotes { get; set; }
    public List<ManualPolicyLineRequest> PolicyLines { get; set; } = new();
    public string DataCompletenessCode { get; set; } = "Partial";
    public string? Agency { get; set; }
    public string? Branch { get; set; }
    public string? Department { get; set; }
    public Guid? ProducerId { get; set; }
    public Guid? AccountManagerId { get; set; }
    public Guid? CsrId { get; set; }
    public string? ProducerName { get; set; }
    public string? AccountManagerName { get; set; }
    public string? CsrName { get; set; }
    public string CommissionTypeCode { get; set; } = "Estimated";
    public decimal? CommissionRate { get; set; }
    public decimal? EstimatedCommission { get; set; }
    public decimal? ProducerSplitPercent { get; set; }
    public string? Notes { get; set; }
    public bool HasSupportingDocument { get; set; }
    public bool OverridePossibleDuplicate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
