namespace Ams.Application.Features.Security;

public sealed class DisableMfaMethodRequest
{
    public Guid MfaDeviceId { get; set; }
    public Guid? DisabledByUserId { get; set; }
}
