namespace Ams.Application.Features.SlaDefinitions;

public sealed class CreateSlaDefinitionRequest
{
    public string  SlaName         { get; set; } = string.Empty;
    public string  ServiceName     { get; set; } = string.Empty;
    public string  MetricTypeCode  { get; set; } = string.Empty;
    public decimal TargetValue     { get; set; } = 99.9m;
    public string  TargetUnit      { get; set; } = "Percent";
    public string  PeriodCode      { get; set; } = "Monthly";
    public string? Notes           { get; set; }
    public Guid?   CreatedByUserId { get; set; }
}

public sealed class UpdateSlaDefinitionRequest
{
    public string  SlaName          { get; set; } = string.Empty;
    public string  ServiceName      { get; set; } = string.Empty;
    public string  MetricTypeCode   { get; set; } = string.Empty;
    public decimal TargetValue      { get; set; }
    public string  TargetUnit       { get; set; } = "Percent";
    public string  PeriodCode       { get; set; } = "Monthly";
    public decimal CurrentValue     { get; set; }
    public string  ComplianceStatus { get; set; } = "Compliant";
    public bool    IsActive         { get; set; }
    public string? Notes            { get; set; }
}
