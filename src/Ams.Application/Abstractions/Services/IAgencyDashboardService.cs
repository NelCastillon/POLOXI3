using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IAgencyDashboardService
{
    Task<AgencyExecutiveOverviewDto> GetExecutiveOverviewAsync(Guid tenantId, CancellationToken ct = default);
    Task<AgencyKpiDto>               GetKpisAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<BranchPerformanceDto>> GetBranchPerformanceAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<ProducerPerformanceDto>> GetProducerPerformanceAsync(Guid tenantId, CancellationToken ct = default);
    Task<RenewalPipelineDto>         GetRenewalPipelineAsync(Guid tenantId, CancellationToken ct = default);
    Task<ClaimsSummaryDto>           GetClaimsSummaryAsync(Guid tenantId, CancellationToken ct = default);
    Task<BillingSummaryDto>          GetBillingSummaryAsync(Guid tenantId, CancellationToken ct = default);
}
