using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class UsageService : IUsageService
{
    private readonly IUsageRepository _repository;

    public UsageService(IUsageRepository repository) => _repository = repository;

    public Task<PlatformUsageDto> GetPlatformUsageAsync(CancellationToken cancellationToken = default)
        => _repository.GetPlatformUsageAsync(cancellationToken);

    public Task<PagedResult<UsageEventDto>> GetUsageEventsAsync(
        Guid?  tenantId      = null,
        string? metricType   = null,
        string? sourceService = null,
        int    pageNumber    = 1,
        int    pageSize      = 50,
        CancellationToken cancellationToken = default)
        => _repository.GetUsageEventsAsync(tenantId, metricType, sourceService, pageNumber, pageSize, cancellationToken);
}
