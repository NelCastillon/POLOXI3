namespace Ams.Application.Features.Security;

using System.ComponentModel.DataAnnotations;

public sealed class AddMfaMethodRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [RegularExpression("SMS", ErrorMessage = "Only SMS phone methods are supported for 2FA.")]
    public string DeviceTypeCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(40)]
    public string? PhoneNumber { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? EmailAddress { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
