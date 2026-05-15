using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Persistence;

public interface IAgreementAmendmentRepository
{
    Task<AgreementAmendmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AgreementAmendmentDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAgreementAmendmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAgreementAmendmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
