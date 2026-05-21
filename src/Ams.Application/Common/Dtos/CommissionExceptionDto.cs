namespace Ams.Application.Common.Dtos;

public sealed class CommissionExceptionDto
{
    public Guid ExceptionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public Guid? CommissionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public Guid? PayoutBatchId { get; set; }
    public string ExceptionNumber { get; set; } = string.Empty;
    public string ExceptionTypeCode { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ImpactAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string StatusCode { get; set; } = string.Empty;
    public string ResolutionNotes { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
