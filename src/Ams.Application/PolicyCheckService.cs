using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Features.PolicyChecks;

namespace Ams.Application;

public sealed class PolicyCheckService : IPolicyCheckService
{
    private readonly IPolicyCheckRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public PolicyCheckService(IPolicyCheckRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task<PolicyCheckCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<PolicyCheckDetailDto?> GetDetailAsync(Guid policyCheckId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(policyCheckId, cancellationToken);

    public async Task<Guid> CreateAsync(CreatePolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: policy checks must stay tenant-safe. When a parent Account is
        // supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "policy check", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid policyCheckId, UpdatePolicyCheckRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(policyCheckId, request, cancellationToken);

    public Task UpdateStatusAsync(Guid policyCheckId, UpdatePolicyCheckStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(policyCheckId, request, cancellationToken);

    public Task UpdateItemAsync(Guid policyCheckItemId, UpdatePolicyCheckItemRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateItemAsync(policyCheckItemId, request, cancellationToken);

    public Task<Guid> AddDiscrepancyAsync(AddPolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
        => _repository.AddDiscrepancyAsync(request, cancellationToken);

    public Task ResolveDiscrepancyAsync(Guid policyCheckDiscrepancyId, ResolvePolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
        => _repository.ResolveDiscrepancyAsync(policyCheckDiscrepancyId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(AddPolicyCheckActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task ArchiveAsync(Guid policyCheckId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.ArchiveAsync(policyCheckId, modifiedByUserId, cancellationToken);
}
