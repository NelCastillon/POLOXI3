using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class AgencyDashboardService : IAgencyDashboardService
{
    private readonly IAgencyDashboardRepository _repo;
    public AgencyDashboardService(IAgencyDashboardRepository repo) => _repo = repo;

    public Task<AgencyExecutiveOverviewDto>   GetExecutiveOverviewAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetExecutiveOverviewAsync(tenantId, ct);
    public Task<AgencyKpiDto>                 GetKpisAsync(Guid tenantId, CancellationToken ct = default)               => _repo.GetKpisAsync(tenantId, ct);
    public Task<List<BranchPerformanceDto>>   GetBranchPerformanceAsync(Guid tenantId, CancellationToken ct = default)  => _repo.GetBranchPerformanceAsync(tenantId, ct);
    public Task<List<ProducerPerformanceDto>> GetProducerPerformanceAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetProducerPerformanceAsync(tenantId, ct);
    public Task<RenewalPipelineDto>           GetRenewalPipelineAsync(Guid tenantId, CancellationToken ct = default)    => _repo.GetRenewalPipelineAsync(tenantId, ct);
    public Task<ClaimsSummaryDto>             GetClaimsSummaryAsync(Guid tenantId, CancellationToken ct = default)      => _repo.GetClaimsSummaryAsync(tenantId, ct);
    public Task<BillingSummaryDto>            GetBillingSummaryAsync(Guid tenantId, CancellationToken ct = default)     => _repo.GetBillingSummaryAsync(tenantId, ct);
}
