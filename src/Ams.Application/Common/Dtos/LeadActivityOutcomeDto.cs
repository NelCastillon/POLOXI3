namespace Ams.Application.Common.Dtos;

public sealed class LeadActivityOutcomeDto
{
    public Guid ActivityOutcomeId { get; set; }
    public Guid TenantId { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    public string OutcomeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
