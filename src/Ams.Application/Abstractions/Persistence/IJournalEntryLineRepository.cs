using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IJournalEntryLineRepository
{
    Task<JournalEntryLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryLineDto>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
}
