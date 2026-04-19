using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Plans;

namespace Ams.Application;

public sealed class PlanSubEntityService : IPlanSubEntityService
{
    private readonly IPlanSubEntityRepository _repo;
    public PlanSubEntityService(IPlanSubEntityRepository repo) => _repo = repo;

    public Task<IReadOnlyList<PlanFeatureDto>> GetFeaturesAsync(Guid planId, CancellationToken ct = default)
        => _repo.GetFeaturesAsync(planId, ct);
    public Task<Guid> AddFeatureAsync(AddPlanFeatureRequest request, CancellationToken ct = default)
        => _repo.AddFeatureAsync(request, ct);
    public Task RemoveFeatureAsync(Guid planFeatureId, CancellationToken ct = default)
        => _repo.RemoveFeatureAsync(planFeatureId, ct);

    public Task<IReadOnlyList<PlanLimitDto>> GetLimitsAsync(Guid planId, CancellationToken ct = default)
        => _repo.GetLimitsAsync(planId, ct);
    public Task<Guid> AddLimitAsync(AddPlanLimitRequest request, CancellationToken ct = default)
        => _repo.AddLimitAsync(request, ct);
    public Task UpdateLimitAsync(UpdatePlanLimitRequest request, CancellationToken ct = default)
        => _repo.UpdateLimitAsync(request, ct);
    public Task RemoveLimitAsync(Guid planLimitId, CancellationToken ct = default)
        => _repo.RemoveLimitAsync(planLimitId, ct);

    public Task<IReadOnlyList<PlanAddOnDto>> GetAddOnsAsync(Guid planId, CancellationToken ct = default)
        => _repo.GetAddOnsAsync(planId, ct);
    public Task<Guid> AddAddOnAsync(AddPlanAddOnRequest request, CancellationToken ct = default)
        => _repo.AddAddOnAsync(request, ct);
    public Task RemoveAddOnAsync(Guid planAddOnId, CancellationToken ct = default)
        => _repo.RemoveAddOnAsync(planAddOnId, ct);
}
