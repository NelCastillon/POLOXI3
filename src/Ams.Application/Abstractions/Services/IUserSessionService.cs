using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IUserSessionService
{
    Task<UserSessionDto?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<PagedResult<UserSessionDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
