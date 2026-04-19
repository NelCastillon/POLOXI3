namespace Ams.Application.Common.Dtos;

public sealed class TenantQuotaDto
{
    public Guid    TenantQuotaId   { get; set; }
    public Guid    TenantId        { get; set; }
    public string  TenantName      { get; set; } = string.Empty;
    public string  MetricTypeCode  { get; set; } = string.Empty;
    public decimal LimitValue      { get; set; }
    public decimal CurrentValue    { get; set; }
    public string  LimitUnit       { get; set; } = string.Empty;
    public string  PeriodCode      { get; set; } = string.Empty;
    public bool    IsEnforced      { get; set; }
    public string  StatusCode      { get; set; } = string.Empty;
    public string? OverrideReason  { get; set; }
    public DateTime?  LastResetDateUtc  { get; set; }
    public DateTime?  NextResetDateUtc  { get; set; }
    public DateTime   CreatedDateUtc    { get; set; }
    public DateTime?  ModifiedDateUtc   { get; set; }
}
