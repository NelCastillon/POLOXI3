using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class ExternalUserProfileService : IExternalUserProfileService
{
    private readonly IExternalUserProfileRepository _repository;
    public ExternalUserProfileService(IExternalUserProfileRepository repository) => _repository = repository;
    public Task<ExternalUserProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<ExternalUserProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => _repository.GetByUserIdAsync(userId, cancellationToken);
    public Task<PagedResult<ExternalUserProfileDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
