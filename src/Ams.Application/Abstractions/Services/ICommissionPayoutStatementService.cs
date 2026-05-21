using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionPayoutStatementService
{
    Task<CommissionPayoutStatementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPayoutStatementDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, Guid? payeeId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GenerateAsync(GenerateCommissionPayoutStatementsRequest request, CancellationToken cancellationToken = default);
}
