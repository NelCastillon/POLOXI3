namespace Ams.Application.Common.Dtos;

public sealed class PolicyRegisterDto
{
    public Guid PolicyId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Commercial";
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal WrittenPremium { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime BoundDateUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string ProducerName { get; set; } = string.Empty;
    public string CsrName { get; set; } = string.Empty;
    public string Branch { get; set; } = "HQ";
    public int DocumentCount { get; set; }
    public int ActivityCount { get; set; }
    public int EndorsementCount { get; set; }
    public string RenewalStage { get; set; } = "Not Started";
    public string LastAction { get; set; } = string.Empty;
}
