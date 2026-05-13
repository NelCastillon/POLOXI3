using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface ICashReceiptEntryRepository
{
    Task<CashReceiptEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CashReceiptEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCashReceiptEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCashReceiptEntryRequest request, CancellationToken cancellationToken = default);
}
