namespace Ams.Application.Common.Dtos;

public sealed class OpportunityConversionLaunchDto
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? LeadConversionId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? SubmissionId { get; set; }
    public string? SourceLeadNumber { get; set; }
    public string? AccountName { get; set; }
    public string? OpportunityName { get; set; }
    public string? OpportunityNumber { get; set; }
    public string? SubmissionNumber { get; set; }
    public string? LineOfBusiness { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public DateTime? ConvertedDateUtc { get; set; }
    public bool HasConversionContext => LeadConversionId.HasValue;
    public bool HasSubmissionDraft => SubmissionId.HasValue;
    public List<OpportunityConversionLaunchActionDto> Actions { get; set; } = [];
}

public sealed class OpportunityConversionLaunchActionDto
{
    public Guid OpportunityConversionLaunchActionId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string ActionTitle { get; set; } = string.Empty;
    public string? ActionDescription { get; set; }
    public string? IconCssClass { get; set; }
    public string? ButtonCssClass { get; set; }
    public string Route { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public bool OpensNewContext { get; set; }
    public bool IsAvailable { get; set; }
}
