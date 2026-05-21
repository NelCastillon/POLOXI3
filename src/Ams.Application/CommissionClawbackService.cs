using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionClawbackService : ICommissionClawbackService
{
    private readonly ICommissionClawbackRepository _repository;

    public CommissionClawbackService(ICommissionClawbackRepository repository) => _repository = repository;

    public Task<CommissionClawbackDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionClawbackDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? reasonCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, reasonCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSeedAsync(tenantId, cancellationToken);
}
