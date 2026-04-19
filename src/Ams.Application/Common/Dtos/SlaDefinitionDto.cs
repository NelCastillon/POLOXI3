namespace Ams.Application.Common.Dtos;

public sealed class SlaDefinitionDto
{
    public Guid    SlaDefinitionId   { get; set; }
    public string  SlaName           { get; set; } = string.Empty;
    public string  ServiceName       { get; set; } = string.Empty;
    public string  MetricTypeCode    { get; set; } = string.Empty;
    public decimal TargetValue       { get; set; }
    public string  TargetUnit        { get; set; } = string.Empty;
    public string  PeriodCode        { get; set; } = string.Empty;
    public decimal CurrentValue      { get; set; }
    public string  ComplianceStatus  { get; set; } = string.Empty;
    public DateTime? LastEvaluatedUtc { get; set; }
    public bool    IsActive          { get; set; }
    public string? Notes             { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
