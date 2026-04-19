using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class BackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobRepository _repository;

    public BackgroundJobService(IBackgroundJobRepository repository) => _repository = repository;

    public Task<PagedResult<BackgroundJobDto>> SearchAsync(string? searchTerm = null, string? jobTypeCode = null, string? statusCode = null, Guid? tenantId = null, bool? failedOnly = null, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, jobTypeCode, statusCode, tenantId, failedOnly, fromDateUtc, toDateUtc, pageNumber, pageSize, cancellationToken);

    public Task<BackgroundJobDto?> GetByIdAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(backgroundJobId, cancellationToken);

    public Task RetryAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
        => _repository.RetryAsync(backgroundJobId, cancellationToken);

    public Task CancelAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
        => _repository.CancelAsync(backgroundJobId, cancellationToken);

    public Task RequeueAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
        => _repository.RequeueAsync(backgroundJobId, cancellationToken);
}
