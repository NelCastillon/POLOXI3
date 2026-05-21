using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Persistence;

public interface IAcordFormRepository
{
    Task<IReadOnlyList<AcordFormDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AcordFormDto?> GetByIdAsync(Guid acordFormId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAcordFormRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(UpdateAcordFormStatusRequest request, CancellationToken cancellationToken = default);
    Task PrefillAsync(PrefillAcordFormRequest request, CancellationToken cancellationToken = default);
}
