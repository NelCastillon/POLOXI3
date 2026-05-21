using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionPlannerScenarioService : ICommissionPlannerScenarioService
{
    private readonly ICommissionPlannerScenarioRepository _repository;

    public CommissionPlannerScenarioService(ICommissionPlannerScenarioRepository repository) => _repository = repository;

    public Task<CommissionPlannerScenarioDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionPlannerScenarioDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, scenarioTypeCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSeedAsync(tenantId, cancellationToken);
}
