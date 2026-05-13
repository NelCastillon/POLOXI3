using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Services;

public interface IExpenseService
{
    Task<ExpenseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ExpenseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateExpenseEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateExpenseEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
