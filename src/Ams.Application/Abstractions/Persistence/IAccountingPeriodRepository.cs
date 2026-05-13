using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccountingPeriodRepository
{
    Task<AccountingPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountingPeriodDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken = default);
}
