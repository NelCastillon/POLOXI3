namespace Ams.Application.Features.Forecast;

public sealed class CreateForecastEntryRequest
{
    public Guid TenantId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string ForecastPeriod { get; set; } = string.Empty;
    public decimal ForecastAmount { get; set; }
    public decimal PipelineAmount { get; set; }
    public string CategoryCode { get; set; } = "Pipeline";
    public DateTime? CloseDate { get; set; }
    public decimal WinProbability { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
