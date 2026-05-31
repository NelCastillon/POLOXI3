using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Features.PolicyCancellations;

namespace Ams.Application;

public sealed class PolicyCancellationService : IPolicyCancellationService
{
    private readonly IPolicyCancellationRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public PolicyCancellationService(IPolicyCancellationRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task<PolicyCancellationCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<PolicyCancellationDetailDto?> GetDetailAsync(Guid cancellationId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(cancellationId, cancellationToken);

    public async Task<Guid> CreateAsync(CreatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a post-policy Cancellation must stay tenant-safe. When a parent
        // Account is supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "cancellation", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid cancellationId, UpdatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(cancellationId, request, cancellationToken);

    public Task UpdateStatusAsync(Guid cancellationId, UpdatePolicyCancellationStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(cancellationId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(AddPolicyCancellationActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task ArchiveAsync(Guid cancellationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.ArchiveAsync(cancellationId, modifiedByUserId, cancellationToken);
}
