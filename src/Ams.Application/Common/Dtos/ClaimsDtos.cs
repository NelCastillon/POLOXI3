namespace Ams.Application.Common.Dtos;

public sealed class ClaimDto
{
    public Guid ClaimId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid AccountId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LossType { get; set; } = string.Empty;
    public string PrimaryClaimant { get; set; } = string.Empty;
    public DateTime DateOfLoss { get; set; }
    public DateTime DateReported { get; set; }
    public DateTime? ClosedDate { get; set; }
    public int DaysOpen { get; set; }
    public decimal TotalIncurred { get; set; }
    public decimal TotalReserves { get; set; }
    public decimal TotalPaid { get; set; }
    public string AssignedHandler { get; set; } = string.Empty;
    public bool IsLitigation { get; set; }
    public bool HasSubrogation { get; set; }
    public bool IsCatastrophe { get; set; }
    public bool IsDisputed { get; set; }
    public string FollowUpReason { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? FollowUpDueDate { get; set; }
    public bool IsSnoozed { get; set; }
    public string? CatCode { get; set; }
    public string? LossLocation { get; set; }
    public string? StateOfLoss { get; set; }
    public string? LossDescription { get; set; }
    public string? CauseOfLoss { get; set; }
    public string? CarrierClaimNumber { get; set; }
    public string? ReportedBy { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ClaimActivityDto
{
    public Guid ClaimActivityId { get; set; }
    public Guid ClaimId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Party { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? PriorAmount { get; set; }
    public DateTime ActivityDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
}

public sealed class ClaimDetailDto
{
    public ClaimDto Claim { get; set; } = new();
    public List<ClaimActivityDto> Activities { get; set; } = [];
}

public sealed class CatEventDto
{
    public Guid CatEventId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CatCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string AffectedStates { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class AffectedInsuredDto
{
    public Guid AffectedInsuredId { get; set; }
    public Guid CatEventId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public decimal TivAtRisk { get; set; }
    public bool GeoTagged { get; set; }
    public bool FnolFiled { get; set; }
    public bool BlastSent { get; set; }
    public string ContactStatus { get; set; } = string.Empty;
    public string Handler { get; set; } = string.Empty;
}

public sealed class CatastrophePageDto
{
    public List<CatEventDto> Events { get; set; } = [];
    public List<AffectedInsuredDto> AffectedInsureds { get; set; } = [];
    public List<ClaimDto> Claims { get; set; } = [];
    public List<ClaimActivityDto> Campaigns { get; set; } = [];
}
