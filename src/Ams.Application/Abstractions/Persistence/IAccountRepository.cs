using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccountRepository
{
    Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
