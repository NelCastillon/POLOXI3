using Ams.Application.Common.Dtos;
using Ams.Application.Features.Plans;

namespace Ams.Application.Abstractions.Services;

public interface IPlanSubEntityService
{
    Task<IReadOnlyList<PlanFeatureDto>> GetFeaturesAsync(Guid planId, CancellationToken ct = default);
    Task<Guid> AddFeatureAsync(AddPlanFeatureRequest request, CancellationToken ct = default);
    Task RemoveFeatureAsync(Guid planFeatureId, CancellationToken ct = default);

    Task<IReadOnlyList<PlanLimitDto>> GetLimitsAsync(Guid planId, CancellationToken ct = default);
    Task<Guid> AddLimitAsync(AddPlanLimitRequest request, CancellationToken ct = default);
    Task UpdateLimitAsync(UpdatePlanLimitRequest request, CancellationToken ct = default);
    Task RemoveLimitAsync(Guid planLimitId, CancellationToken ct = default);

    Task<IReadOnlyList<PlanAddOnDto>> GetAddOnsAsync(Guid planId, CancellationToken ct = default);
    Task<Guid> AddAddOnAsync(AddPlanAddOnRequest request, CancellationToken ct = default);
    Task RemoveAddOnAsync(Guid planAddOnId, CancellationToken ct = default);
}
