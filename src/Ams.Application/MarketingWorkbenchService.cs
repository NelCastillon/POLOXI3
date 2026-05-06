using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class MarketingWorkbenchService : IMarketingWorkbenchService
{
    private readonly IMarketingWorkbenchRepository _repository;

    public MarketingWorkbenchService(IMarketingWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<MarketingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, teamScope, branchId, teamId, cancellationToken);

    public Task ApproveContentAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
        => _repository.ApproveContentAsync(tenantId, itemId, cancellationToken);
}
