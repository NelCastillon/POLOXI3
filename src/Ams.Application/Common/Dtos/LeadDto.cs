namespace Ams.Application.Common.Dtos;

public sealed class LeadDto
{
    public Guid LeadId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AccountId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? InterestedService { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? Score { get; set; }
    public string? PriorityCode { get; set; }
    public string? SourceCode { get; set; }
    public string? NurturingStageCode { get; set; }
    public DateTime? QualifiedDate { get; set; }
    public int StatusCode { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
