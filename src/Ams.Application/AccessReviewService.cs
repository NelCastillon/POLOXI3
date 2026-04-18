using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;

namespace Ams.Application;

public sealed class AccessReviewService : IAccessReviewService
{
    private readonly IAccessReviewRepository _repository;
    public AccessReviewService(IAccessReviewRepository repository) => _repository = repository;

    public Task<UserAccessReviewDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<UserAccessReviewDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    // ── Campaigns ─────────────────────────────────────────────────────────────
    public Task<AccessReviewCampaignDto?> GetCampaignByIdAsync(Guid id, CancellationToken ct = default) => _repository.GetCampaignByIdAsync(id, ct);
    public Task<PagedResult<AccessReviewCampaignDto>> SearchCampaignsAsync(Guid tenantId, string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => _repository.SearchCampaignsAsync(tenantId, searchTerm, statusCode, pageNumber, pageSize, ct);
    public Task<Guid> CreateCampaignAsync(CreateAccessReviewCampaignRequest request, CancellationToken ct = default) => _repository.CreateCampaignAsync(request, ct);
    public Task UpdateCampaignAsync(Guid id, UpdateAccessReviewCampaignRequest request, CancellationToken ct = default) => _repository.UpdateCampaignAsync(id, request, ct);
    public Task ChangeCampaignStatusAsync(Guid id, string newStatusCode, Guid changedByUserId, CancellationToken ct = default) => _repository.ChangeCampaignStatusAsync(id, newStatusCode, changedByUserId, ct);
    public Task<IReadOnlyList<AccessReviewItemDto>> GetItemsAsync(Guid campaignId, CancellationToken ct = default) => _repository.GetItemsAsync(campaignId, ct);
    public Task SubmitDecisionAsync(Guid campaignId, Guid itemId, SubmitReviewDecisionRequest request, CancellationToken ct = default) => _repository.SubmitDecisionAsync(campaignId, itemId, request, ct);
}
