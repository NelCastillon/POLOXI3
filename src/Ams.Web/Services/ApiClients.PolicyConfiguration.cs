using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<CoverageTypeDto>?> SearchCoverageTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CoverageTypeDto>>($"api/policies/coverage-types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateCoverageTypeAsync(CreateCoverageTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/coverage-types", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateCoverageTypeAsync(Guid id, UpdateCoverageTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/coverage-types/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCoverageTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/policies/coverage-types/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PolicyStatusDto>?> SearchPolicyStatusesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PolicyStatusDto>>($"api/policies/statuses?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePolicyStatusAsync(CreatePolicyStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/statuses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePolicyStatusAsync(Guid id, UpdatePolicyStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/statuses/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePolicyStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/policies/statuses/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<EndorsementTypeDto>?> SearchEndorsementTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<EndorsementTypeDto>>($"api/policies/endorsement-types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateEndorsementTypeAsync(CreateEndorsementTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/endorsement-types", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateEndorsementTypeAsync(Guid id, UpdateEndorsementTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/endorsement-types/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEndorsementTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/policies/endorsement-types/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CancellationReasonDto>?> SearchCancellationReasonsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CancellationReasonDto>>($"api/policies/cancellation-reasons?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateCancellationReasonAsync(CreateCancellationReasonRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/cancellation-reasons", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateCancellationReasonAsync(Guid id, UpdateCancellationReasonRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/cancellation-reasons/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCancellationReasonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/policies/cancellation-reasons/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<CertificateSettingDto>?> GetCertificateSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<CertificateSettingDto>>($"api/policies/certificate-settings?tenantId={tenantId}", cancellationToken);

    public async Task UpdateCertificateSettingAsync(Guid id, UpdateCertificateSettingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/certificate-settings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<IdCardSettingDto>?> GetIdCardSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<IdCardSettingDto>>($"api/policies/id-card-settings?tenantId={tenantId}", cancellationToken);

    public async Task UpdateIdCardSettingAsync(Guid id, UpdateIdCardSettingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/id-card-settings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PolicyCustomFieldDto>?> SearchPolicyCustomFieldsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PolicyCustomFieldDto>>($"api/policies/custom-fields?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePolicyCustomFieldAsync(CreatePolicyCustomFieldRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/policies/custom-fields", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePolicyCustomFieldAsync(Guid id, UpdatePolicyCustomFieldRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/policies/custom-fields/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePolicyCustomFieldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/policies/custom-fields/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
