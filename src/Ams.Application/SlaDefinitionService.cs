using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SlaDefinitions;

namespace Ams.Application;

public sealed class SlaDefinitionService : ISlaDefinitionService
{
    private readonly ISlaDefinitionRepository _repository;

    public SlaDefinitionService(ISlaDefinitionRepository repository) => _repository = repository;

    public Task<PagedResult<SlaDefinitionDto>> SearchAsync(string? searchTerm, string? complianceStatus, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, complianceStatus, pageNumber, pageSize, cancellationToken);

    public Task<SlaDefinitionDto?> GetByIdAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(slaDefinitionId, cancellationToken);

    public Task<Guid> CreateAsync(CreateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid slaDefinitionId, UpdateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(slaDefinitionId, request, cancellationToken);

    public Task DeleteAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(slaDefinitionId, cancellationToken);
}
