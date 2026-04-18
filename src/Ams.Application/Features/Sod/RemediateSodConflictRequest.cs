namespace Ams.Application.Features.Sod;

public sealed class RemediateSodConflictRequest
{
    public Guid?   RemediatedByUserId { get; set; }
    public string? RemediationNote    { get; set; }
}
