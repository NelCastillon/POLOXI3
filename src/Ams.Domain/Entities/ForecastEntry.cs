namespace Ams.Domain.Entities;

public sealed class ForecastEntry
{
    public Guid ForecastEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string ForecastPeriod { get; set; } = string.Empty;
    public decimal ForecastAmount { get; set; }
    public decimal PipelineAmount { get; set; }
    public string CategoryCode { get; set; } = "Pipeline";
    public DateOnly? CloseDate { get; set; }
    public decimal WinProbability { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
