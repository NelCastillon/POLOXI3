using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class UserScopeService : IUserScopeService
{
    private readonly IUserScopeRepository _repository;
    public UserScopeService(IUserScopeRepository repository) => _repository = repository;
    public Task<PagedResult<UserScopeDto>> SearchAsync(Guid tenantId, Guid? userId, string? scopeTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, userId, scopeTypeCode, pageNumber, pageSize, cancellationToken);
    public Task<IEnumerable<UserScopeDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) => _repository.GetByUserAsync(userId, cancellationToken);
    public Task<Guid> AssignAsync(AssignUserScopeRequest request, CancellationToken cancellationToken = default) => _repository.AssignAsync(request, cancellationToken);
    public Task RevokeAsync(Guid userScopeId, Guid? revokedByUserId, CancellationToken cancellationToken = default) => _repository.RevokeAsync(userScopeId, revokedByUserId, cancellationToken);
}
