using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Duplicates;
using Ams.Application.Features.SearchMatching;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ams.Application;

public sealed class DuplicateService : IDuplicateService
{
    private readonly IDuplicateRepository _repository;
    private readonly IEntityMatchingService _matchingService;

    public DuplicateService(IDuplicateRepository repository, IEntityMatchingService matchingService)
    {
        _repository = repository;
        _matchingService = matchingService;
    }

    public Task<PagedResult<DuplicateGroupDto>> SearchAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(request, cancellationToken);

    public async Task<int> ScanAsync(DuplicateScanRequest request, CancellationToken cancellationToken = default)
    {
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Account"] = MatchProfileCodes.AccountDuplicate,
            ["Contact"] = MatchProfileCodes.ContactDuplicate,
            ["Lead"] = MatchProfileCodes.LeadDuplicate
        };
        var sources = await _repository.GetScanSourcesAsync(request.TenantId, cancellationToken);
        foreach (var source in sources)
        {
            if (!profiles.TryGetValue(source.EntityTypeCode, out var profileCode)) continue;
            await _matchingService.FindMatchesAsync(new EntityMatchRequest
            {
                TenantId = request.TenantId,
                ProfileCode = profileCode,
                EntityTypeCode = source.EntityTypeCode,
                SourceEntityId = source.EntityId,
                CorrelationId = $"duplicate-scan:{source.EntityTypeCode}:{source.EntityId:N}:{SourceHash(source.Fields)}",
                RequestedByUserId = request.ScannedByUserId,
                Fields = source.Fields
            }, cancellationToken);
        }
        return await _repository.ScanAsync(request, cancellationToken);
    }

    private static string SourceHash(IReadOnlyDictionary<string, string?> fields)
    {
        var json = JsonSerializer.Serialize(fields.OrderBy(field => field.Key, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
    }

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
