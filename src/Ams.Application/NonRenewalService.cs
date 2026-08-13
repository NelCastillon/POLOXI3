using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Features.NonRenewals;

namespace Ams.Application;

public sealed class NonRenewalService : INonRenewalService
{
    private readonly INonRenewalRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public NonRenewalService(INonRenewalRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task<NonRenewalCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<NonRenewalDetailDto?> GetDetailAsync(Guid nonRenewalId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(nonRenewalId, cancellationToken);

    public async Task<Guid> CreateAsync(CreateNonRenewalRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: non-renewals must stay tenant-safe. When a parent Account is
        // supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "non-renewal", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid nonRenewalId, UpdateNonRenewalRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(nonRenewalId, request, cancellationToken);

    public Task UpdateStatusAsync(Guid nonRenewalId, UpdateNonRenewalStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(nonRenewalId, request, cancellationToken);

    public Task RecordInsuredNotificationAsync(Guid nonRenewalId, RecordInsuredNotificationRequest request, CancellationToken cancellationToken = default)
        => _repository.RecordInsuredNotificationAsync(nonRenewalId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(AddNonRenewalActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task ArchiveAsync(Guid nonRenewalId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.ArchiveAsync(nonRenewalId, modifiedByUserId, cancellationToken);
}
