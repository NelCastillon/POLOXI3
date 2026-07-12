using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingAccounts;

namespace Ams.Application;

public sealed class BillingAccountService : IBillingAccountService
{
    private readonly IBillingAccountRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public BillingAccountService(IBillingAccountRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task EnsureSchemaAndSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

    public Task<BillingAccountDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(accountId, cancellationToken);

    public Task<PagedResult<BillingAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyList<BillingModeDashboardRowDto>> GetBillingModeDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetBillingModeDashboardAsync(tenantId, cancellationToken);

    public async Task<Guid> CreateAsync(CreateBillingAccountRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Billing Account must never be orphaned. It requires a parent
        // Account within the same tenant.
        await TenantGuard.EnsureParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "billing account", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid accountId, UpdateBillingAccountRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(accountId, request, cancellationToken);

    public Task DeleteAsync(Guid accountId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(accountId, modifiedByUserId, cancellationToken);
}
