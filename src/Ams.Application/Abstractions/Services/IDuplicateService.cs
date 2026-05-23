using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Duplicates;

namespace Ams.Application.Abstractions.Services;

public interface IDuplicateService
{
    Task<PagedResult<DuplicateGroupDto>> SearchAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default);

    Task<int> ScanAsync(DuplicateScanRequest request, CancellationToken cancellationToken = default);

    Task SetPrimaryAsync(Guid groupId, DuplicateSetPrimaryRequest request, CancellationToken cancellationToken = default);

    Task MergeAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default);

    Task DismissAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default);

    Task BulkMergeAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default);

    Task BulkDismissAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default);
}
