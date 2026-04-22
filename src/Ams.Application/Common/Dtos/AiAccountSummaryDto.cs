namespace Ams.Application.Common.Dtos;

public sealed class AiAccountSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string HealthIndicator { get; set; } = string.Empty;
    public string[] KeyRisks { get; set; } = [];
    public string[] Opportunities { get; set; } = [];
    public DateTime GeneratedDateUtc { get; set; }
}
