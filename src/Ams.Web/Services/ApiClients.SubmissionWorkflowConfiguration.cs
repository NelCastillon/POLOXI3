using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<SubmissionWorkflowConfigurationSummaryDto?> GetSubmissionWorkflowConfigurationSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SubmissionWorkflowConfigurationSummaryDto>($"api/submissions/workflow-configuration/summary?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<SubmissionIntakeTemplateDto>?> GetSubmissionIntakeTemplatesAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SubmissionIntakeTemplateDto>>($"api/submissions/workflow-configuration/intake-templates?tenantId={tenantId}&lineOfBusiness={Uri.EscapeDataString(lineOfBusiness ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateSubmissionIntakeTemplateAsync(UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/submissions/workflow-configuration/intake-templates", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSubmissionIntakeTemplateAsync(Guid intakeTemplateId, UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/submissions/workflow-configuration/intake-templates/{intakeTemplateId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteSubmissionIntakeTemplateAsync(Guid intakeTemplateId, Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var url = userId.HasValue
            ? $"api/submissions/workflow-configuration/intake-templates/{intakeTemplateId}?tenantId={tenantId}&userId={userId.Value}"
            : $"api/submissions/workflow-configuration/intake-templates/{intakeTemplateId}?tenantId={tenantId}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<SubmissionDocumentRequirementDto>?> GetSubmissionDocumentRequirementsAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SubmissionDocumentRequirementDto>>($"api/submissions/workflow-configuration/document-requirements?tenantId={tenantId}&lineOfBusiness={Uri.EscapeDataString(lineOfBusiness ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateSubmissionDocumentRequirementAsync(UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/submissions/workflow-configuration/document-requirements", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSubmissionDocumentRequirementAsync(Guid documentRequirementId, UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/submissions/workflow-configuration/document-requirements/{documentRequirementId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteSubmissionDocumentRequirementAsync(Guid documentRequirementId, Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var url = userId.HasValue
            ? $"api/submissions/workflow-configuration/document-requirements/{documentRequirementId}?tenantId={tenantId}&userId={userId.Value}"
            : $"api/submissions/workflow-configuration/document-requirements/{documentRequirementId}?tenantId={tenantId}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }
}
