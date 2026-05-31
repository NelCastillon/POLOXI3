using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class AgreementRenewalService : IAgreementRenewalService
{
    private readonly IAgreementRenewalRepository _repository;
    private readonly IAgreementRepository _agreementRepository;

    public AgreementRenewalService(IAgreementRenewalRepository repository, IAgreementRepository agreementRepository)
    {
        _repository = repository;
        _agreementRepository = agreementRepository;
    }

    public Task<AgreementRenewalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<AgreementRenewalDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, agreementId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> CreateAsync(CreateAgreementRenewalRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Renewal must never be orphaned. It requires a parent Agreement
        // within the same tenant.
        await TenantGuard.EnsureParentAsync(request.AgreementId, request.TenantId, _agreementRepository.GetByIdAsync, a => a.TenantId, "Agreement", "renewal", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }
}
