using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentWorkflow;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<DocumentWorkflowTemplateDto>?> SearchDocumentWorkflowTemplatesAsync(Guid tenantId, string? workflowType = null, bool? isActive = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentWorkflowTemplateDto>>($"api/document-workflow/templates?tenantId={tenantId}&workflowType={Uri.EscapeDataString(workflowType ?? string.Empty)}&isActive={isActive}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<IReadOnlyList<DocumentWorkflowTemplateDto>?> GetActiveDocumentWorkflowTemplatesAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentWorkflowTemplateDto>>($"api/document-workflow/templates/active?tenantId={tenantId}", ct);

    public async Task<Guid> CreateDocumentWorkflowTemplateAsync(CreateWorkflowTemplateRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/document-workflow/templates", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<Guid>(cancellationToken: ct));
    }

    public async Task UpdateDocumentWorkflowTemplateAsync(Guid id, UpdateWorkflowTemplateRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/document-workflow/templates/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteDocumentWorkflowTemplateAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/document-workflow/templates/{id}?modifiedByUserId={modifiedByUserId}", ct)).EnsureSuccessStatusCode();

    public Task<IReadOnlyList<DocumentWorkflowStepTemplateDto>?> GetDocumentWorkflowTemplateStepsAsync(Guid workflowTemplateId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentWorkflowStepTemplateDto>>($"api/document-workflow/templates/{workflowTemplateId}/steps", ct);

    public Task<PagedResult<DocumentWorkflowInstanceDto>?> SearchDocumentWorkflowInstancesAsync(Guid tenantId, string? workflowStatus = null, Guid? documentId = null, Guid? initiatedByUserId = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentWorkflowInstanceDto>>($"api/document-workflow/instances?tenantId={tenantId}&workflowStatus={Uri.EscapeDataString(workflowStatus ?? string.Empty)}&documentId={documentId}&initiatedByUserId={initiatedByUserId}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<IReadOnlyList<DocumentWorkflowInstanceDto>?> GetActiveDocumentWorkflowInstancesAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentWorkflowInstanceDto>>($"api/document-workflow/instances/active?tenantId={tenantId}", ct);

    public Task<PagedResult<DocumentApprovalDto>?> SearchDocumentApprovalsAsync(Guid tenantId, string? approvalStatus = null, Guid? assignedToUserId = null, Guid? documentId = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentApprovalDto>>($"api/document-workflow/approvals?tenantId={tenantId}&approvalStatus={Uri.EscapeDataString(approvalStatus ?? string.Empty)}&assignedToUserId={assignedToUserId}&documentId={documentId}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<PagedResult<DocumentReviewDto>?> SearchDocumentReviewsAsync(Guid tenantId, string? reviewStatus = null, Guid? assignedToUserId = null, Guid? documentId = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentReviewDto>>($"api/document-workflow/reviews?tenantId={tenantId}&reviewStatus={Uri.EscapeDataString(reviewStatus ?? string.Empty)}&assignedToUserId={assignedToUserId}&documentId={documentId}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<PagedResult<DocumentRetentionPolicyDto>?> SearchDocumentRetentionPoliciesAsync(Guid tenantId, bool? isActive = null, string? applicableCategory = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentRetentionPolicyDto>>($"api/document-workflow/retention-policies?tenantId={tenantId}&isActive={isActive}&applicableCategory={Uri.EscapeDataString(applicableCategory ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public async Task<Guid> CreateDocumentRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/document-workflow/retention-policies", request, ct);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Guid>(cancellationToken: ct);
    }

    public async Task UpdateDocumentRetentionPolicyAsync(Guid id, UpdateRetentionPolicyRequest request, CancellationToken ct = default)
        => (await _httpClient.PutAsJsonAsync($"api/document-workflow/retention-policies/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteDocumentRetentionPolicyAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default)
        => (await _httpClient.DeleteAsync($"api/document-workflow/retention-policies/{id}?modifiedByUserId={modifiedByUserId}", ct)).EnsureSuccessStatusCode();

    public Task<PagedResult<DocumentAuditTrailDto>?> SearchDocumentWorkflowAuditTrailAsync(Guid tenantId, Guid? documentId = null, Guid? workflowInstanceId = null, string? eventType = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentAuditTrailDto>>($"api/document-workflow/audit-trail?tenantId={tenantId}&documentId={documentId}&workflowInstanceId={workflowInstanceId}&eventType={Uri.EscapeDataString(eventType ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);

    public Task<PagedResult<DocumentClassificationQueueDto>?> SearchDocumentClassificationQueueAsync(Guid tenantId, string? queueStatus = null, Guid? assignedToUserId = null, string? priority = null, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentClassificationQueueDto>>($"api/document-workflow/classification?tenantId={tenantId}&queueStatus={Uri.EscapeDataString(queueStatus ?? string.Empty)}&assignedToUserId={assignedToUserId}&priority={Uri.EscapeDataString(priority ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
}
