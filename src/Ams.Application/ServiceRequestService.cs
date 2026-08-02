using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;
    public ServiceRequestService(IServiceRequestRepository repository) => _repository = repository;
    public Task<ServiceRequestDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(tenantId, id, cancellationToken);
    public Task<PagedResult<ServiceRequestDto>> SearchAsync(Guid tenantId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, accountId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateServiceRequestRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task DeleteAsync(Guid tenantId, Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.DeleteAsync(tenantId, id, modifiedByUserId, cancellationToken);
}
