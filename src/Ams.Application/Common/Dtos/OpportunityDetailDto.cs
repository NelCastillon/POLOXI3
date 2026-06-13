namespace Ams.Application.Common.Dtos;

public sealed class OpportunityDetailDto
{
    public OpportunityDto Opportunity { get; set; } = new();
    public IReadOnlyList<OpportunityLineDto> Lines { get; set; } = [];
    public IReadOnlyList<OpportunityActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<OpportunitySubmissionDto> Submissions { get; set; } = [];
    public IReadOnlyList<QuoteDto> Quotes { get; set; } = [];
    public IReadOnlyList<OpportunityCompetitorDto> Competitors { get; set; } = [];
    public IReadOnlyList<OpportunityWorkflowEventDto> WorkflowEvents { get; set; } = [];
}

public sealed class OpportunityLineDto
{
    public Guid OpportunityLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public decimal EstPremium { get; set; }
    public string Priority { get; set; } = "Medium";
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class OpportunityWorkflowEventDto
{
    public Guid WorkflowEventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string? EventDetail { get; set; }
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime EventDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class OpportunityActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class OpportunitySubmissionDto
{
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public decimal TargetPremium { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class OpportunityCompetitorDto
{
    public Guid CompetitorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Strength { get; set; } = "Moderate";
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
