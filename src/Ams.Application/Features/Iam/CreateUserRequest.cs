using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Iam;

public sealed class CreateUserRequest
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? UserNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string UserTypeCode { get; set; } = "Internal";
    public string StatusCode { get; set; } = "Active";
    [AmsPhone]
    public string? PhoneNumber { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Department { get; set; }
    public string? Region { get; set; }
    public Guid? JobTitleId { get; set; }
    public string? JobTitle { get; set; }
    public string? TimeZoneCode { get; set; }
    public string? LocaleCode { get; set; }
    public bool MfaRequired { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
