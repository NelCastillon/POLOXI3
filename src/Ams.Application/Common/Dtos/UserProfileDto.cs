namespace Ams.Application.Common.Dtos;

public sealed class UserProfileDto
{
    public Guid    UserId                { get; set; }
    public string? PhoneNumber           { get; set; }
    public string? MobileNumber          { get; set; }
    public string? CountryCode           { get; set; }
    public string? AddressLine1          { get; set; }
    public string? AddressLine2          { get; set; }
    public string? City                  { get; set; }
    public string? StateProvince         { get; set; }
    public string? PostalCode            { get; set; }
    public string? AvatarUrl             { get; set; }
    public string? EmergencyContactName  { get; set; }
    public string? EmergencyContactPhone { get; set; }
}
