using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;

namespace Ams.Application.Abstractions.Services;

public interface IAccessReviewService
{
    Task<UserAccessReviewDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserAccessReviewDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── Campaigns ─────────────────────────────────────────────────────────────
    Task<AccessReviewCampaignDto?> GetCampaignByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AccessReviewCampaignDto>> SearchCampaignsAsync(Guid tenantId, string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default);
    Task<Guid> CreateCampaignAsync(CreateAccessReviewCampaignRequest request, CancellationToken ct = default);
    Task UpdateCampaignAsync(Guid id, UpdateAccessReviewCampaignRequest request, CancellationToken ct = default);
    Task ChangeCampaignStatusAsync(Guid id, string newStatusCode, Guid changedByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessReviewItemDto>> GetItemsAsync(Guid campaignId, CancellationToken ct = default);
    Task SubmitDecisionAsync(Guid campaignId, Guid itemId, SubmitReviewDecisionRequest request, CancellationToken ct = default);
}
