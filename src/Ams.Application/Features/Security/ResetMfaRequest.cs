namespace Ams.Application.Features.Security;

public sealed class ResetMfaRequest
{
    public Guid UserId { get; set; }
    public Guid? ResetByUserId { get; set; }
}
