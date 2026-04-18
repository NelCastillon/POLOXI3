using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class AgreementRenewalService : IAgreementRenewalService
{
    private readonly IAgreementRenewalRepository _repository;
    public AgreementRenewalService(IAgreementRenewalRepository repository) => _repository = repository;
    public Task<AgreementRenewalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<AgreementRenewalDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, agreementId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateAgreementRenewalRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
}
