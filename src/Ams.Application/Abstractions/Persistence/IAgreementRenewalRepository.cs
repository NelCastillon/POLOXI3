using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Persistence;

public interface IAgreementRenewalRepository
{
    Task<AgreementRenewalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AgreementRenewalDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAgreementRenewalRequest request, CancellationToken cancellationToken = default);
}
