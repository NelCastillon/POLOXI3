using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class UserGroupService : IUserGroupService
{
    private readonly IUserGroupRepository _repository;
    public UserGroupService(IUserGroupRepository repository) => _repository = repository;
    public Task<UserGroupDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<UserGroupDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<PagedResult<UserGroupMemberDto>> SearchMembersAsync(Guid tenantId, Guid? userGroupId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchMembersAsync(tenantId, userGroupId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> AddMemberAsync(AddUserGroupMemberRequest request, CancellationToken cancellationToken = default) => _repository.AddMemberAsync(request, cancellationToken);
    public Task RemoveMemberAsync(Guid memberId, Guid? removedByUserId, CancellationToken cancellationToken = default) => _repository.RemoveMemberAsync(memberId, removedByUserId, cancellationToken);
}
