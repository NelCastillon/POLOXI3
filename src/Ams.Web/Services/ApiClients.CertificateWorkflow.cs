using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCertificates;
using System.Net.Http.Json;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<CertificateWorkflowWorkspaceDto?> GetCertificateWorkflowWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<CertificateWorkflowWorkspaceDto>($"api/certificate-workflow/workspace?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<CertificateAuditEventDto>?> GetCertificateAuditAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CertificateAuditEventDto>>($"api/certificate-workflow/certificates/{certificateId}/audit?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<CertificateDeliveryDto>?> GetCertificateDeliveriesAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CertificateDeliveryDto>>($"api/certificate-workflow/certificates/{certificateId}/deliveries?tenantId={tenantId}", cancellationToken);

    public async Task<Guid?> GetLatestGeneratedCertificateDocumentVersionIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/certificate-workflow/certificates/{certificateId}/latest-document-version?tenantId={tenantId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid?>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> UpsertCertificateHolderAsync(UpsertCertificateHolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/certificate-workflow/holders", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> CreateDocumentTemplateVersionAsync(Guid templateDefinitionId, CreateDocumentTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/certificate-workflow/templates/{templateDefinitionId}/versions", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> CreateCertificateWorkflowRequestAsync(CreateCertificateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/certificate-workflow/requests", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<(byte[] Content, string ContentType)> GenerateCertificateDocumentAsync(Guid certificateId, GenerateCertificateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/certificate-workflow/certificates/{certificateId}/generate", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadAsByteArrayAsync(cancellationToken), response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    public async Task<Guid> QueueCertificateDeliveryAsync(Guid certificateId, QueueCertificateDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/certificate-workflow/certificates/{certificateId}/deliveries", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<Guid> UpsertCertificateRenewalScheduleAsync(Guid certificateId, UpsertCertificateRenewalScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/certificate-workflow/certificates/{certificateId}/renewal-schedule", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }
}
