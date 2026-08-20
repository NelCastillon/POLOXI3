using Ams.Application.Common.Dtos;
using Ams.Application.Features.RenewalRetention;

namespace Ams.Application.Abstractions.Services;

public interface IRenewalRetentionService
{
    Task<RenewalRetentionCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<RenewalInitiationResultDto> InitiateEligibleAsync(InitiateEligibleRenewalsRequest request, CancellationToken cancellationToken = default);
    Task LaunchPlacementAsync(Guid retentionCaseId, LaunchRenewalPlacementRequest request, CancellationToken cancellationToken = default);
    Task<RenewalRetentionDetailDto?> GetDetailAsync(Guid retentionCaseId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCaseAsync(CreateRenewalRetentionCaseRequest request, CancellationToken cancellationToken = default);
    Task UpdateStageAsync(Guid retentionCaseId, UpdateRenewalRetentionStageRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(CreateRenewalRetentionActivityRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddOfferAsync(CreateRenewalRetentionOfferRequest request, CancellationToken cancellationToken = default);
    Task UpdateOfferStatusAsync(Guid retentionOfferId, UpdateRenewalRetentionOfferStatusRequest request, CancellationToken cancellationToken = default);
}
