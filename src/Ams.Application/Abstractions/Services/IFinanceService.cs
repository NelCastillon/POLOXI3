using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IFinanceService
{
    Task<GLAccountDto?> GetGLAccountByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<GLAccountDto>> SearchGLAccountsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryDto>> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
