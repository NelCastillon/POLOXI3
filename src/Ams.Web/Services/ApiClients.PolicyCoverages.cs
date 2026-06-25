using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCoverages;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<IReadOnlyList<PolicyCoverageDetailDto>?> GetPolicyCoverageDetailsAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyCoverageDetailDto>>($"api/policies/coverages?tenantId={tenantId}&policyId={policyId}", cancellationToken);

    public Task<IReadOnlyList<PolicyCoverageDetailTemplateDto>?> GetPolicyCoverageDetailTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyCoverageDetailTemplateDto>>($"api/policies/coverages/templates?tenantId={tenantId}", cancellationToken);

    public Task<PolicyCoverageDetailDto?> GetPolicyCoverageDetailByCodeAsync(Guid tenantId, Guid policyId, string coverageCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCoverageDetailDto>($"api/policies/coverages/by-code?tenantId={tenantId}&policyId={policyId}&coverageCode={Uri.EscapeDataString(coverageCode)}", cancellationToken);

    public Task<PolicyCoverageDetailDto?> GetPolicyCoverageDetailByIdAsync(Guid tenantId, Guid coverageDetailId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyCoverageDetailDto>($"api/policies/coverages/{coverageDetailId}?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreatePolicyCoverageDetailAsync(CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/coverages", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePolicyCoverageDetailAsync(Guid coverageDetailId, UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/coverages/{coverageDetailId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePolicyCoverageDetailAsync(Guid tenantId, Guid coverageDetailId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/policies/coverages/{coverageDetailId}?tenantId={tenantId}";
        if (modifiedByUserId.HasValue)
        {
            url += $"&modifiedByUserId={modifiedByUserId.Value}";
        }

        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreatePolicyCoverageFieldAsync(CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/coverages/fields", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePolicyCoverageFieldAsync(Guid fieldId, UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/coverages/fields/{fieldId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePolicyCoverageFieldAsync(Guid tenantId, Guid fieldId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/policies/coverages/fields/{fieldId}?tenantId={tenantId}";
        if (modifiedByUserId.HasValue)
        {
            url += $"&modifiedByUserId={modifiedByUserId.Value}";
        }

        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
