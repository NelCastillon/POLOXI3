using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IUsageRepository
{
    Task<PlatformUsageDto> GetPlatformUsageAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<UsageEventDto>> GetUsageEventsAsync(
        Guid?  tenantId      = null,
        string? metricType   = null,
        string? sourceService = null,
        int    pageNumber    = 1,
        int    pageSize      = 50,
        CancellationToken cancellationToken = default);
}
