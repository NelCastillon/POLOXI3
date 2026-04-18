namespace Ams.Application.Features.Security;

public sealed class RequireMfaRequest
{
    public Guid UserId { get; set; }
    public bool IsRequired { get; set; }
    public Guid? SetByUserId { get; set; }
}
