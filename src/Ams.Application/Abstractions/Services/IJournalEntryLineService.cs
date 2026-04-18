using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IJournalEntryLineService
{
    Task<JournalEntryLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryLineDto>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
}
