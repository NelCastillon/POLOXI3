using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Iam;

public sealed class UpdateUserProfileRequest
{
    [Required]
    public Guid    UserId                { get; set; }

    [Phone]
    [StringLength(40)]
    public string? PhoneNumber           { get; set; }

    [Phone]
    [StringLength(40)]
    public string? MobileNumber          { get; set; }

    [StringLength(10)]
    public string? CountryCode           { get; set; }

    [StringLength(300)]
    public string? AddressLine1          { get; set; }

    [StringLength(300)]
    public string? AddressLine2          { get; set; }

    [StringLength(150)]
    public string? City                  { get; set; }

    [StringLength(150)]
    public string? StateProvince         { get; set; }

    [StringLength(30)]
    public string? PostalCode            { get; set; }

    [Url]
    [StringLength(1000)]
    public string? AvatarUrl             { get; set; }

    [StringLength(30)]
    public string? AvatarColor           { get; set; }

    [StringLength(200)]
    public string? EmergencyContactName  { get; set; }

    [Phone]
    [StringLength(40)]
    public string? EmergencyContactPhone { get; set; }
}
