using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IJournalEntryRepository
{
    Task<JournalEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken = default);
}
