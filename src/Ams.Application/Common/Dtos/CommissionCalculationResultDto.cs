namespace Ams.Application.Common.Dtos;

public sealed class CommissionCalculationResultDto
{
    public Guid CalculationResultId { get; set; }
    public Guid TenantId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid PayeeId { get; set; }
    public Guid CommissionPlanId { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal RatePct { get; set; }
    public decimal SplitPct { get; set; }
    public decimal CalculatedAmount { get; set; }
    public decimal? AdjustedAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CalculatedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
