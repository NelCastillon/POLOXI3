using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class OperationalActivityService : IOperationalActivityService
{
    private readonly IOperationalActivityRepository _repository;
    public OperationalActivityService(IOperationalActivityRepository repository) => _repository = repository;
    public Task<OperationalActivityLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<OperationalActivityLogDto>> SearchAsync(Guid tenantId, Guid? accountId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, accountId, engagementId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateOperationalActivityRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
}
