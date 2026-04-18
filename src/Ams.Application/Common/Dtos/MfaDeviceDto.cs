namespace Ams.Application.Common.Dtos;

public sealed class MfaDeviceDto
{
    public Guid MfaDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string DeviceTypeCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
