namespace Ams.Application.Common.Dtos;

public sealed class OpportunityDto
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public string OpportunityNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public int StatusCode { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? CloseDate { get; set; }
    public decimal WinProbability { get; set; }
    public string? ForecastCategoryCode { get; set; }
    public Guid? LeadId { get; set; }
    public string? StageName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public string? OwnerName { get; set; }
    public string? SourceLead { get; set; }
}
