using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IMarketingWorkbenchRepository
{
    Task<MarketingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
    Task ApproveContentAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);
}
