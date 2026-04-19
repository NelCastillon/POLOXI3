namespace Ams.Application.Features.Alerts;

public sealed class AcknowledgeAlertRequest
{
    public Guid?   AcknowledgedByUserId { get; set; }
    public string? Notes                { get; set; }
}

public sealed class ResolveAlertRequest
{
    public Guid?   ResolvedByUserId { get; set; }
    public string? Notes            { get; set; }
}

public sealed class AssignAlertRequest
{
    public Guid?   OwnerUserId { get; set; }
    public string? Notes       { get; set; }
}

public sealed class EscalateAlertRequest
{
    public string? SeverityCode { get; set; }
    public string? Notes        { get; set; }
}
