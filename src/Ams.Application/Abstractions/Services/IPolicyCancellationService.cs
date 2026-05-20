using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCancellations;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyCancellationService
{
    Task<PolicyCancellationCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyCancellationDetailDto?> GetDetailAsync(Guid cancellationId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyCancellationRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid cancellationId, UpdatePolicyCancellationRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid cancellationId, UpdatePolicyCancellationStatusRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddPolicyCancellationActivityRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid cancellationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
