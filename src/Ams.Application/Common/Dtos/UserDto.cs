namespace Ams.Application.Common.Dtos;

public sealed class UserDto
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? UserNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string UserTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool MfaEnabled { get; set; }
    public DateTime? LastLoginDateUtc { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TimeZoneCode { get; set; }
    public string? LocaleCode { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public DateTime? PasswordChangedDateUtc { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? LockoutEndDateUtc { get; set; }
    public int FailedLoginAttempts { get; set; }
    public int AssignedRoleCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
