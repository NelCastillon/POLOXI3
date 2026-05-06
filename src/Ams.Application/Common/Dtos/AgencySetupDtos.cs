namespace Ams.Application.Common.Dtos;

public sealed class AgencyDepartmentDto
{
    public Guid DepartmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public int TeamCount { get; set; }
    public int UserCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class AgencyTeamDto
{
    public Guid TeamId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public string? TeamType { get; set; }
    public int MemberCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class AgencyStaffDto
{
    public Guid StaffId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Team { get; set; }
    public bool IsActive { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
