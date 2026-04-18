using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IUserGroupService
{
    Task<UserGroupDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserGroupDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<UserGroupMemberDto>> SearchMembersAsync(Guid tenantId, Guid? userGroupId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> AddMemberAsync(AddUserGroupMemberRequest request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid memberId, Guid? removedByUserId, CancellationToken cancellationToken = default);
}
