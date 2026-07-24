using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;

namespace Ams.Application;

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;

    public AccountService(IAccountRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, userId, cancellationToken);

    public Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _repository.GetContactsByAccountIdAsync(accountId, cancellationToken);

    public Task<Account360Dto?> GetAccount360Async(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default)
        => _repository.GetAccount360Async(tenantId, accountId, cancellationToken);

    public Task<Guid> UpsertNamedInsuredAsync(UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken = default) => _repository.UpsertNamedInsuredAsync(request, cancellationToken);
    public Task<Guid> UpsertLocationAsync(UpsertAccountLocationRequest request, CancellationToken cancellationToken = default) => _repository.UpsertLocationAsync(request, cancellationToken);
    public Task<Guid> UpsertVehicleAsync(UpsertAccountVehicleRequest request, CancellationToken cancellationToken = default) => _repository.UpsertVehicleAsync(request, cancellationToken);
    public Task<Guid> UpsertDriverAsync(UpsertAccountDriverRequest request, CancellationToken cancellationToken = default) => _repository.UpsertDriverAsync(request, cancellationToken);
    public Task<Guid> UpsertPropertyAsync(UpsertAccountPropertyRequest request, CancellationToken cancellationToken = default) => _repository.UpsertPropertyAsync(request, cancellationToken);
    public Task<Guid> UpsertScheduleItemAsync(UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken = default) => _repository.UpsertScheduleItemAsync(request, cancellationToken);
    public Task DeleteAccount360ItemAsync(Guid tenantId, Guid accountId, string entityType, Guid entityId, Guid? userId, CancellationToken cancellationToken = default) => _repository.DeleteAccount360ItemAsync(tenantId, accountId, entityType, entityId, userId, cancellationToken);
}
