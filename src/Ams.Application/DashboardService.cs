using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repository;
    public DashboardService(IDashboardRepository repository) => _repository = repository;
    public Task<DashboardKpiDto> GetKpiAsync(Guid tenantId, CancellationToken cancellationToken = default) => _repository.GetKpiAsync(tenantId, cancellationToken);
}
