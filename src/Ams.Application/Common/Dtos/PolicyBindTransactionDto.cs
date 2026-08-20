namespace Ams.Application.Common.Dtos;

public sealed class PolicyBindTransactionDto
{
    public Guid PolicyBindTransactionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid QuoteId { get; set; }
    public string? QuoteNumber { get; set; }
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string PolicySourceCode { get; set; } = "QuoteBound";
    public string PolicySourceName { get; set; } = "Quote Bound";
    public string BindStatusCode { get; set; } = "Bound";
    public string BindStatusName { get; set; } = "Bound";
    public string? BindReason { get; set; }
    public string? Notes { get; set; }
    public Guid? ClientAcceptanceId { get; set; }
    public string? BindingAuthorityCode { get; set; }
    public string? BindingAuthorityName { get; set; }
    public string? BindingMethodCode { get; set; }
    public string? BindingMethodName { get; set; }
    public string? ProducerNotes { get; set; }
    public string? CarrierInstructions { get; set; }
    public string? SpecialConditions { get; set; }
    public bool ApprovalRequired { get; set; }
    public bool PaymentRequired { get; set; }
    public bool PaymentVerified { get; set; }
    public decimal AnnualPremium { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public TimeSpan? RequestedEffectiveTime { get; set; }
    public string? ConfirmationSourceCode { get; set; }
    public string? ConfirmationSourceName { get; set; }
    public string? CarrierReferenceNumber { get; set; }
    public string? BinderNumber { get; set; }
    public decimal? FinalPremium { get; set; }
    public decimal? DownPaymentAmount { get; set; }
    public string? SubjectivitiesOutstanding { get; set; }
    public string? ConfirmationNotes { get; set; }
    public Guid? ConfirmationDocumentId { get; set; }
    public string? ConfirmationReceivedFrom { get; set; }
    public string? ConfirmationMessageId { get; set; }
    public Guid? UnderwriterContactId { get; set; }
    public string? UnderwriterName { get; set; }
    public string? UnderwriterCompany { get; set; }
    public Guid? CommissionPlanApplicabilityId { get; set; }
    public Guid? CommissionPlanId { get; set; }
    public Guid? CommissionPlanVersionId { get; set; }
    public Guid? CommissionPayeeId { get; set; }
    public Guid? CommissionSplitRuleId { get; set; }
    public string? CommissionBusinessTypeCode { get; set; }
    public decimal? CommissionRatePct { get; set; }
    public decimal? CommissionSplitPct { get; set; }
    public decimal? CommissionablePremium { get; set; }
    public decimal? EstimatedGrossCommission { get; set; }
    public decimal? EstimatedProducerCommission { get; set; }
    public bool FollowUpWrittenConfirmationRequired { get; set; }
    public string? IntegrationCorrelationId { get; set; }
    public string? ExternalTransactionId { get; set; }
    public bool ConfirmedManually { get; set; }
    public bool ConfirmationCertified { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime RequestedDateUtc { get; set; }
    public DateTime? PreparedDateUtc { get; set; }
    public DateTime? SubmittedDateUtc { get; set; }
    public DateTime? ReceivedDateUtc { get; set; }
    public DateTime? ResponseDueDateUtc { get; set; }
    public int RetryCount { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public Guid? BoundByUserId { get; set; }
    public DateTime? BoundDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class BindCommissionEstimateDto
{
    public bool IsConfigured { get; set; }
    public string? UnavailableReason { get; set; }
    public Guid? CommissionPlanApplicabilityId { get; set; }
    public Guid? CommissionPlanId { get; set; }
    public string? CommissionPlanName { get; set; }
    public Guid? CommissionPlanVersionId { get; set; }
    public int? PlanVersionNumber { get; set; }
    public Guid? CommissionPayeeId { get; set; }
    public Guid? CommissionSplitRuleId { get; set; }
    public string BusinessTypeCode { get; set; } = "NewBusiness";
    public decimal CommissionRatePct { get; set; }
    public decimal CommissionSplitPct { get; set; }
    public decimal CommissionablePremium { get; set; }
    public decimal EstimatedGrossCommission { get; set; }
    public decimal EstimatedProducerCommission { get; set; }
}
