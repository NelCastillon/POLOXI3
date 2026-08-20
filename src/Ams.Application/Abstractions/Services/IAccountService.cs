using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;

namespace Ams.Application.Abstractions.Services;

public interface IAccountService
{
    Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<Account360Dto?> GetAccount360Async(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default);
    Task ReplaceServiceAssignmentsAsync(ReplaceAccountServiceAssignmentsRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertNamedInsuredAsync(UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertLocationAsync(UpsertAccountLocationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertVehicleAsync(UpsertAccountVehicleRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertDriverAsync(UpsertAccountDriverRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertPropertyAsync(UpsertAccountPropertyRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertScheduleItemAsync(UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteAccount360ItemAsync(Guid tenantId, Guid accountId, string entityType, Guid entityId, Guid? userId, CancellationToken cancellationToken = default);
}
