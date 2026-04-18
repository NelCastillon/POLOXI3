using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IExpenseRepository
{
    Task<ExpenseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ExpenseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
