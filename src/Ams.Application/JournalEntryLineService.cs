using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class JournalEntryLineService : IJournalEntryLineService
{
    private readonly IJournalEntryLineRepository _repository;
    public JournalEntryLineService(IJournalEntryLineRepository repository) => _repository = repository;
    public Task<JournalEntryLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<IReadOnlyList<JournalEntryLineDto>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => _repository.GetByJournalEntryIdAsync(journalEntryId, cancellationToken);
}
