using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface IAgreementAmendmentService
{
    Task<AgreementAmendmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AgreementAmendmentDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAgreementAmendmentRequest request, CancellationToken cancellationToken = default);
}
