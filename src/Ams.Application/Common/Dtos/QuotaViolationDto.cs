namespace Ams.Application.Common.Dtos;

public sealed class QuotaViolationDto
{
    public Guid    ViolationId          { get; set; }
    public Guid    TenantId             { get; set; }
    public string  TenantName           { get; set; } = string.Empty;
    public string  MetricTypeCode       { get; set; } = string.Empty;
    public DateTime ViolationDateUtc    { get; set; }
    public decimal LimitValue           { get; set; }
    public decimal ActualValue          { get; set; }
    public decimal ExcessValue          { get; set; }
    public string  SeverityCode         { get; set; } = string.Empty;
    public string  StatusCode           { get; set; } = string.Empty;
    public string? Notes                { get; set; }
    public Guid?   AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedDateUtc { get; set; }
    public Guid?   ResolvedByUserId     { get; set; }
    public DateTime? ResolvedDateUtc    { get; set; }
    public DateTime  CreatedDateUtc     { get; set; }
}
