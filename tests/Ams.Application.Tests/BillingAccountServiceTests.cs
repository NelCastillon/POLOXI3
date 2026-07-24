using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.BillingAccounts;
using Xunit;

namespace Ams.Application.Tests;

public sealed class BillingAccountServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreateAsync_Throws_When_Account_Missing()
    {
        var service = new BillingAccountService(new FakeBillingAccountRepository(), new FakeAccountRepository(null));
        var request = new CreateBillingAccountRequest { TenantId = TenantA, AccountId = Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_Throws_When_Account_Cross_Tenant()
    {
        var accountId = Guid.NewGuid();
        var account = new AccountDto { AccountId = accountId, TenantId = TenantB };
        var service = new BillingAccountService(new FakeBillingAccountRepository(), new FakeAccountRepository(account));
        var request = new CreateBillingAccountRequest { TenantId = TenantA, AccountId = accountId };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));

        Assert.Contains("different tenant", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_Throws_When_AccountId_Empty()
    {
        var service = new BillingAccountService(new FakeBillingAccountRepository(), new FakeAccountRepository(null));
        var request = new CreateBillingAccountRequest { TenantId = TenantA, AccountId = Guid.Empty };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_Persists_When_Account_Valid()
    {
        var accountId = Guid.NewGuid();
        var account = new AccountDto { AccountId = accountId, TenantId = TenantA };
        var billingRepo = new FakeBillingAccountRepository();
        var service = new BillingAccountService(billingRepo, new FakeAccountRepository(account));
        var request = new CreateBillingAccountRequest { TenantId = TenantA, AccountId = accountId };

        var result = await service.CreateAsync(request);

        Assert.Equal(billingRepo.LastCreatedId, result);
        Assert.Same(request, billingRepo.LastCreatedRequest);
    }

    private sealed class FakeBillingAccountRepository : IBillingAccountRepository
    {
        public Guid LastCreatedId { get; } = Guid.NewGuid();
        public CreateBillingAccountRequest? LastCreatedRequest { get; private set; }

        public Task EnsureSchemaAndSeedAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BillingAccountDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<BillingAccountDto?>(null);
        public Task<PagedResult<BillingAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<BillingAccountDto>());

        public Task<IReadOnlyList<BillingModeDashboardRowDto>> GetBillingModeDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BillingModeDashboardRowDto>>([]);

        public Task<Guid> CreateAsync(CreateBillingAccountRequest request, CancellationToken cancellationToken = default)
        {
            LastCreatedRequest = request;
            return Task.FromResult(LastCreatedId);
        }

        public Task UpdateAsync(Guid accountId, UpdateBillingAccountRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid accountId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly AccountDto? _account;
        public FakeAccountRepository(AccountDto? account) => _account = account;

        public Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_account);
        public Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AccountDto>());
        public Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContactDto>>([]);
        public Task<Account360Dto?> GetAccount360Async(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<Account360Dto?>(null);
        public Task<Guid> UpsertNamedInsuredAsync(UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertLocationAsync(UpsertAccountLocationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertVehicleAsync(UpsertAccountVehicleRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertDriverAsync(UpsertAccountDriverRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertPropertyAsync(UpsertAccountPropertyRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertScheduleItemAsync(UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task DeleteAccount360ItemAsync(Guid tenantId, Guid accountId, string entityType, Guid entityId, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AccountDto>> FindMatchCandidatesAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountDto>>([]);
    }
}
