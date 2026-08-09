using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PolicyEndorsementCenterDto?> GetPolicyEndorsementCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementCenterDto>($"api/policy-endorsements/center?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<PolicyEndorsementOptionDto>?> GetPolicyEndorsementOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyEndorsementOptionDto>>($"api/policy-endorsements/options?tenantId={tenantId}", cancellationToken);

    public Task<PolicyEndorsementCatalogDto?> GetPolicyEndorsementCatalogAsync(Guid tenantId, string? lineOfBusinessCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementCatalogDto>($"api/policy-endorsements/catalog?tenantId={tenantId}&lineOfBusinessCode={Uri.EscapeDataString(lineOfBusinessCode ?? string.Empty)}", cancellationToken);

    public Task<PolicyEndorsementTypeCatalogDto?> GetPolicyEndorsementTypeCatalogAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementTypeCatalogDto>($"api/policy-endorsements/catalog/{Uri.EscapeDataString(typeCode)}?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<PolicyEndorsementTypeCatalogDto>?> GetAvailablePolicyEndorsementTypesAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyEndorsementTypeCatalogDto>>($"api/policy-endorsements/policies/{policyId}/available-types?tenantId={tenantId}", cancellationToken);

    public Task<PolicyEndorsementTypeCatalogDto?> GetPolicyEndorsementRequirementsAsync(Guid tenantId, Guid policyId, string typeCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementTypeCatalogDto>($"api/policy-endorsements/types/{Uri.EscapeDataString(typeCode)}/requirements?tenantId={tenantId}&policyId={policyId}", cancellationToken);

    public async Task UpdatePolicyEndorsementTypeProfileAsync(Guid endorsementTypeId, UpdatePolicyEndorsementTypeProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-endorsements/catalog/{endorsementTypeId}/profile", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReplacePolicyEndorsementTypeConfigurationAsync(Guid endorsementTypeId, ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-endorsements/catalog/{endorsementTypeId}/configuration", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PolicyEndorsementDetailDto?> GetPolicyEndorsementDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementDetailDto>($"api/policy-endorsements/{endorsementId}?tenantId={tenantId}", cancellationToken);

    public Task<PolicyEndorsementWorkflowDetailDto?> GetPolicyEndorsementWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementWorkflowDetailDto>($"api/policy-endorsements/{endorsementId}/workflow?tenantId={tenantId}", cancellationToken);

    public async Task<PolicyEndorsementRoutePreviewDto?> GetPolicyEndorsementRoutePreviewAsync(Guid tenantId, Guid endorsementId, string purpose, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/policy-endorsements/{endorsementId}/route-preview?tenantId={tenantId}&purpose={Uri.EscapeDataString(purpose)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("The running API does not expose workflow route previews. Restart the API application so it loads the latest endpoint changes.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PolicyEndorsementRoutePreviewDto>(cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PolicyEndorsementApprovalInboxItemDto>?> GetPolicyEndorsementApprovalInboxAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyEndorsementApprovalInboxItemDto>>($"api/policy-endorsements/approval-inbox?tenantId={tenantId}", cancellationToken);

    public Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyEndorsementWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyEndorsementPolicyWorkspaceDto>($"api/policy-endorsements/policies/{policyId}/workspace?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreatePolicyEndorsementTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-endorsements/transactions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task SavePolicyEndorsementDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-endorsements/{endorsementId}/draft", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> RequestPolicyEndorsementInformationAsync(Guid endorsementId, RequestPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/information-requests", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task RespondToPolicyEndorsementInformationRequestAsync(Guid endorsementId, Guid informationRequestId, RespondPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/information-requests/{informationRequestId}/response", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResubmitPolicyEndorsementInformationRequestAsync(Guid endorsementId, Guid informationRequestId, ResubmitPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/information-requests/{informationRequestId}/resubmission", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TransitionPolicyEndorsementAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/transitions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DecidePolicyEndorsementApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/approvals/{approvalId}/decision", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> ReversePolicyEndorsementAsync(Guid endorsementId, ReversePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/{endorsementId}/reversal", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> CreatePolicyEndorsementAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policy-endorsements", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdatePolicyEndorsementAsync(Guid tenantId, Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policy-endorsements/{endorsementId}?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePolicyEndorsementStatusAsync(Guid tenantId, Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/policy-endorsements/{endorsementId}/status?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPolicyEndorsementActivityAsync(Guid tenantId, AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/activities?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> UpsertPolicyEndorsementDeltaAsync(Guid tenantId, UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/policy-endorsements/deltas?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchivePolicyEndorsementAsync(Guid tenantId, Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var query = $"?tenantId={tenantId}";
        if (modifiedByUserId.HasValue) query += $"&modifiedByUserId={modifiedByUserId.Value}";
        var response = await _httpClient.DeleteAsync($"api/policy-endorsements/{endorsementId}{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
