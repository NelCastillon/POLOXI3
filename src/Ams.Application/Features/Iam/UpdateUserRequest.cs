namespace Ams.Application.Features.Iam;

public sealed class UpdateUserRequest
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? Region { get; set; }
    public string? JobTitle { get; set; }
    public string? TimeZoneCode { get; set; }
    public string? LocaleCode { get; set; }
    public bool? MfaRequired { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
