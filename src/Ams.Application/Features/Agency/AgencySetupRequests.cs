using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Agency;

public sealed record CreateAgencyDepartmentRequest(
    Guid TenantId,
    Guid? BranchId,
    [property: Required, StringLength(50)] string DepartmentCode,
    [property: Required, StringLength(200)] string DepartmentName,
    [property: StringLength(1000)] string? Description,
    Guid? ManagerUserId,
    bool IsActive = true);

public sealed record UpdateAgencyDepartmentRequest(
    Guid? BranchId,
    [property: Required, StringLength(50)] string DepartmentCode,
    [property: Required, StringLength(200)] string DepartmentName,
    [property: StringLength(1000)] string? Description,
    Guid? ManagerUserId,
    bool IsActive);

public sealed record CreateAgencyTeamRequest(
    Guid TenantId,
    Guid? DepartmentId,
    [property: Required, StringLength(50)] string TeamCode,
    [property: Required, StringLength(200)] string TeamName,
    [property: StringLength(1000)] string? Description,
    Guid? ManagerUserId,
    [property: StringLength(100)] string? TeamType,
    bool IsActive = true);

public sealed record UpdateAgencyTeamRequest(
    Guid? DepartmentId,
    [property: Required, StringLength(50)] string TeamCode,
    [property: Required, StringLength(200)] string TeamName,
    [property: StringLength(1000)] string? Description,
    Guid? ManagerUserId,
    [property: StringLength(100)] string? TeamType,
    bool IsActive);

public sealed record UpsertAgencyStaffRequest(
    Guid TenantId,
    Guid? UserId,
    Guid? BranchId,
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, EmailAddress, StringLength(200)] string Email,
    [property: StringLength(50)] string? Phone,
    [property: StringLength(100)] string? Title,
    [property: Required, StringLength(100)] string Role,
    [property: StringLength(200)] string? Department,
    [property: StringLength(200)] string? Team,
    bool IsActive = true);
