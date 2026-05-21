using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionExceptionService : ICommissionExceptionService
{
    private readonly ICommissionExceptionRepository _repository;

    public CommissionExceptionService(ICommissionExceptionRepository repository) => _repository = repository;

    public Task<CommissionExceptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionExceptionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? severityCode = null, string? typeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, severityCode, typeCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSeedAsync(tenantId, cancellationToken);
}
