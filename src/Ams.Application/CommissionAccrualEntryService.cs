using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionAccrualEntryService : ICommissionAccrualEntryService
{
    private readonly ICommissionAccrualEntryRepository _repository;

    public CommissionAccrualEntryService(ICommissionAccrualEntryRepository repository) => _repository = repository;

    public Task<CommissionAccrualEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionAccrualEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSeedAsync(tenantId, cancellationToken);
}
