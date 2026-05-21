using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionForecastService : ICommissionForecastService
{
    private readonly ICommissionForecastRepository _repository;

    public CommissionForecastService(ICommissionForecastRepository repository) => _repository = repository;

    public Task<CommissionForecastDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionForecastDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, scenarioCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommissionForecastRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionForecastRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSeedAsync(tenantId, cancellationToken);
}
