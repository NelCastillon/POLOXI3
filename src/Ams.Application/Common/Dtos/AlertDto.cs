namespace Ams.Application.Common.Dtos;

public sealed class AlertDto
{
    public Guid    AlertId             { get; set; }
    public string  AlertName           { get; set; } = string.Empty;
    public string  AlertTypeCode       { get; set; } = "System";
    public string  ServiceName         { get; set; } = string.Empty;
    public string  SeverityCode        { get; set; } = string.Empty;
    public string  StatusCode          { get; set; } = string.Empty;
    public string? RegionCode          { get; set; }
    public Guid?   TenantId            { get; set; }
    public Guid?   OwnerUserId         { get; set; }
    public string? Message             { get; set; }
    public DateTime TriggeredDateUtc   { get; set; }
    public Guid?   AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedDateUtc { get; set; }
    public Guid?   ResolvedByUserId    { get; set; }
    public DateTime? ResolvedDateUtc   { get; set; }
    public DateTime? EscalatedDateUtc  { get; set; }
    public string? Notes               { get; set; }
    public DateTime CreatedDateUtc     { get; set; }
}
