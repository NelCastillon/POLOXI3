using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Application;

public sealed class PolicyEndorsementService : IPolicyEndorsementService
{
    private readonly IPolicyEndorsementRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public PolicyEndorsementService(IPolicyEndorsementRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid endorsementId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(endorsementId, cancellationToken);

    public async Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a post-policy Endorsement must stay tenant-safe. When a parent
        // Account is supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "endorsement", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(endorsementId, request, cancellationToken);

    public Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(endorsementId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertDeltaAsync(request, cancellationToken);

    public Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.ArchiveAsync(endorsementId, modifiedByUserId, cancellationToken);
}
