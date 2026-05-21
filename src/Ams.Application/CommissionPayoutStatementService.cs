using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application;

public sealed class CommissionPayoutStatementService : ICommissionPayoutStatementService
{
    private readonly ICommissionPayoutStatementRepository _repository;

    public CommissionPayoutStatementService(ICommissionPayoutStatementRepository repository) => _repository = repository;

    public Task<CommissionPayoutStatementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<CommissionPayoutStatementDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, Guid? payeeId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, statusCode, payeeId, pageNumber, pageSize, cancellationToken);

    public async Task EnsureSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureSeedAsync(tenantId, createdByUserId, cancellationToken);
    }

    public Task<Guid> CreateAsync(CreateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task<IReadOnlyList<Guid>> GenerateAsync(GenerateCommissionPayoutStatementsRequest request, CancellationToken cancellationToken = default)
        => _repository.GenerateAsync(request, cancellationToken);
}
