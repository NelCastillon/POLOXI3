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
    public Task<ServiceRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<ServiceRequestDto>> SearchAsync(Guid tenantId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, accountId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
}
