using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class ServiceIssueService : IServiceIssueService
{
    private readonly IServiceIssueRepository _repository;
    public ServiceIssueService(IServiceIssueRepository repository) => _repository = repository;
    public Task<ServiceIssueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<ServiceIssueDto>> SearchAsync(Guid tenantId, Guid? engagementId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, engagementId, accountId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateServiceIssueRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
}
