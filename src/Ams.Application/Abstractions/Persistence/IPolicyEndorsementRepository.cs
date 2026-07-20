using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Application.Abstractions.Persistence;

public interface IPolicyEndorsementRepository
{
    Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid endorsementId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
