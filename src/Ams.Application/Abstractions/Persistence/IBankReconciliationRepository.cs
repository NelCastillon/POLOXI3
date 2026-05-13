using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IBankReconciliationRepository
{
    Task<BankReconciliationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BankReconciliationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateBankReconciliationRequest request, CancellationToken cancellationToken = default);
}
