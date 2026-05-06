using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IUserSessionRepository
{
    Task<UserSessionDto?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<PagedResult<UserSessionDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(Guid tenantId, Guid? userId = null, string? reason = null, CancellationToken cancellationToken = default);
}
