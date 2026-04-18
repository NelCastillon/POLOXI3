namespace Ams.Application.Features.Security;

public sealed class AddMfaMethodRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string DeviceTypeCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
