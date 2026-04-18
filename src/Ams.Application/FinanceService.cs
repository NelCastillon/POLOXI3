using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class FinanceService : IFinanceService
{
    private readonly IGLAccountRepository _glRepo;
    private readonly IJournalEntryRepository _jeRepo;

    public FinanceService(IGLAccountRepository glRepo, IJournalEntryRepository jeRepo)
    {
        _glRepo = glRepo;
        _jeRepo = jeRepo;
    }

    public Task<GLAccountDto?> GetGLAccountByIdAsync(Guid id, CancellationToken cancellationToken = default) => _glRepo.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<GLAccountDto>> SearchGLAccountsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _glRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default) => _jeRepo.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<JournalEntryDto>> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _jeRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
