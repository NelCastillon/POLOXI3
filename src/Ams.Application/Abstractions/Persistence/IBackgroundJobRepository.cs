using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IBackgroundJobRepository
{
    Task<PagedResult<BackgroundJobDto>> SearchAsync(string? searchTerm = null, string? jobTypeCode = null, string? statusCode = null, Guid? tenantId = null, bool? failedOnly = null, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<BackgroundJobDto?> GetByIdAsync(Guid backgroundJobId, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid backgroundJobId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid backgroundJobId, CancellationToken cancellationToken = default);
    Task RequeueAsync(Guid backgroundJobId, CancellationToken cancellationToken = default);
}
