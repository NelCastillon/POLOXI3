namespace Ams.Application.Features.Security;

public sealed class RevokeTrustedDeviceRequest
{
    public Guid TrustedDeviceId { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? Reason { get; set; }
}
