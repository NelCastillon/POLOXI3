namespace Ams.Application.Common.Dtos;

public sealed class AiInsightDto
{
    public Guid InsightId { get; set; }
    public Guid TenantId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ActionableRecommendation { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime GeneratedDateUtc { get; set; }
}
