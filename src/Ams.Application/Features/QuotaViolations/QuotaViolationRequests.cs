namespace Ams.Application.Features.QuotaViolations;

public sealed class AcknowledgeQuotaViolationRequest
{
    public Guid?   AcknowledgedByUserId { get; set; }
    public string? Notes                { get; set; }
}

public sealed class ResolveQuotaViolationRequest
{
    public Guid?   ResolvedByUserId { get; set; }
    public string? Notes            { get; set; }
}

public sealed class NotifyQuotaViolationRequest
{
    public Guid?   NotifiedByUserId { get; set; }
    public string? Notes            { get; set; }
}

public sealed class ApplyRestrictionRequest
{
    public Guid?   AppliedByUserId { get; set; }
    public string? Notes           { get; set; }
}

public sealed class GrantTemporaryIncreaseRequest
{
    public Guid?    GrantedByUserId { get; set; }
    public decimal  IncreaseAmount  { get; set; }
    public string?  Notes           { get; set; }
}

public sealed class ConvertToOverageRequest
{
    public Guid?   ConvertedByUserId { get; set; }
    public string? Notes             { get; set; }
}
