namespace Ams.Application.Features.Security;

public sealed class ChangeUserStatusRequest
{
    public Guid    UserId        { get; set; }
    public string  NewStatus     { get; set; } = string.Empty;
    public string? Reason        { get; set; }
    public string? EffectiveNote { get; set; }
    public Guid?   ChangedByUserId { get; set; }
}
