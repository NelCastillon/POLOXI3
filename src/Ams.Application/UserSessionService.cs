using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class UserSessionService : IUserSessionService
{
    private readonly IUserSessionRepository _repository;

    public UserSessionService(IUserSessionRepository repository)
        => _repository = repository;

    public Task<UserSessionDto?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(sessionId, cancellationToken);

    public Task<PagedResult<UserSessionDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, userId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task RevokeAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
        => _repository.RevokeAsync(sessionId, reason, cancellationToken);

    public Task RevokeAllAsync(Guid tenantId, Guid? userId = null, string? reason = null, CancellationToken cancellationToken = default)
        => _repository.RevokeAllAsync(tenantId, userId, reason, cancellationToken);
}
