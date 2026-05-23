using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Duplicates;

namespace Ams.Application;

public sealed class DuplicateService : IDuplicateService
{
    private readonly IDuplicateRepository _repository;

    public DuplicateService(IDuplicateRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<DuplicateGroupDto>> SearchAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(request, cancellationToken);

    public Task<int> ScanAsync(DuplicateScanRequest request, CancellationToken cancellationToken = default)
        => _repository.ScanAsync(request, cancellationToken);

    public Task SetPrimaryAsync(Guid groupId, DuplicateSetPrimaryRequest request, CancellationToken cancellationToken = default)
        => _repository.SetPrimaryAsync(groupId, request, cancellationToken);

    public Task MergeAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
        => _repository.MergeAsync(groupId, request, cancellationToken);

    public Task DismissAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
        => _repository.DismissAsync(groupId, request, cancellationToken);

    public Task BulkMergeAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
        => _repository.BulkMergeAsync(request, cancellationToken);

    public Task BulkDismissAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
        => _repository.BulkDismissAsync(request, cancellationToken);
}
