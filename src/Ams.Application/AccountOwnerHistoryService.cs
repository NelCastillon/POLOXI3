using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class AccountOwnerHistoryService : IAccountOwnerHistoryService
{
    private readonly IAccountOwnerHistoryRepository _repository;

    public AccountOwnerHistoryService(IAccountOwnerHistoryRepository repository) => _repository = repository;

    public Task<AccountOwnerHistoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AccountOwnerHistoryDto>> SearchAsync(Guid tenantId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, accountId, searchTerm, pageNumber, pageSize, cancellationToken);
}
