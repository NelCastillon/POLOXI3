using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Claims;

namespace Ams.Application.Abstractions.Services;

public interface IClaimsService
{
    Task<PagedResult<ClaimDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lob, string? catCode, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, CancellationToken cancellationToken = default);
    Task UpdateFollowUpAsync(Guid claimId, UpdateClaimFollowUpRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(CreateClaimActivityRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<CatEventDto>> SearchCatEventsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCatEventAsync(CreateCatEventRequest request, CancellationToken cancellationToken = default);
    Task<CatastrophePageDto> GetCatastrophePageAsync(Guid tenantId, Guid? catEventId, CancellationToken cancellationToken = default);
    Task MarkAffectedInsuredContactedAsync(Guid affectedInsuredId, CancellationToken cancellationToken = default);
    Task<int> ApplyGeoTagAsync(Guid catEventId, string? states, string? counties, string? zips, string? lob, decimal? minTiv, CancellationToken cancellationToken = default);
    Task<int> SendCatBlastAsync(CatBlastRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateFastCatFnolAsync(FastCatFnolRequest request, CancellationToken cancellationToken = default);
}
