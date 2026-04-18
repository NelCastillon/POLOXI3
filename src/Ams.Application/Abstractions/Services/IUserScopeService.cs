using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IUserScopeService
{
    Task<PagedResult<UserScopeDto>> SearchAsync(Guid tenantId, Guid? userId, string? scopeTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserScopeDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> AssignAsync(AssignUserScopeRequest request, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userScopeId, Guid? revokedByUserId, CancellationToken cancellationToken = default);
}
