namespace Ams.Application.Features.Security;

public sealed class VerifyMfaMethodRequest
{
    public Guid MfaDeviceId { get; set; }
    public Guid? VerifiedByUserId { get; set; }
}
