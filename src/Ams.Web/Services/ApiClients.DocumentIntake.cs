using System.Net;
using System.Net.Http.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<DocumentIntakeSessionDto>?> SearchDocumentIntakeAsync(string? searchTerm=null,string? moduleCode=null,string? statusCode=null,Guid? assignedToUserId=null,Guid? targetEntityId=null,int pageNumber=1,int pageSize=50,CancellationToken cancellationToken=default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentIntakeSessionDto>>($"api/document-intake?searchTerm={Uri.EscapeDataString(searchTerm??string.Empty)}&moduleCode={Uri.EscapeDataString(moduleCode??string.Empty)}&statusCode={Uri.EscapeDataString(statusCode??string.Empty)}&assignedToUserId={assignedToUserId}&targetEntityId={targetEntityId}&pageNumber={pageNumber}&pageSize={pageSize}",cancellationToken);

    public async Task<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>> GetDocumentIntakeStatusesAsync(string moduleCode,Guid targetEntityId,CancellationToken cancellationToken=default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>>($"api/document-intake/document-statuses?moduleCode={Uri.EscapeDataString(moduleCode)}&targetEntityId={targetEntityId}",cancellationToken)??[];

    public async Task<DocumentIntakeDetailDto?> GetDocumentIntakeAsync(Guid id,CancellationToken cancellationToken=default)
    {
        using var response=await _httpClient.GetAsync($"api/document-intake/{id}",cancellationToken);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<DocumentIntakeDetailDto>(cancellationToken:cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentIntakeDeadLetterDto>> GetDocumentIntakeDeadLettersAsync(int pageSize=100,CancellationToken cancellationToken=default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DocumentIntakeDeadLetterDto>>($"api/document-intake/operations/dead-letters?pageSize={pageSize}",cancellationToken)??[];
    public Task ReplayDocumentIntakeDeadLetterAsync(Guid workItemId,ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken=default)
        => PostNoContentAsync($"api/document-intake/operations/dead-letters/{workItemId}/replay",command,cancellationToken);
    public async Task<IReadOnlyCollection<DocumentIntakePromptSuiteDto>> GetDocumentIntakePromptSuitesAsync(CancellationToken cancellationToken=default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DocumentIntakePromptSuiteDto>>("api/document-intake/operations/prompt-suites",cancellationToken)??[];
    public async Task<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>> GetDocumentIntakePromptRunsAsync(int pageSize=100,CancellationToken cancellationToken=default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>>($"api/document-intake/operations/prompt-runs?pageSize={pageSize}",cancellationToken)??[];
    public async Task<IReadOnlyCollection<DocumentIntakeAlertDto>> GetDocumentIntakeAlertsAsync(bool openOnly=true,CancellationToken cancellationToken=default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DocumentIntakeAlertDto>>($"api/document-intake/operations/alerts?openOnly={openOnly}",cancellationToken)??[];
    public Task<DocumentIntakeRuntimeSettings?> GetDocumentIntakeRuntimeSettingsAsync(CancellationToken cancellationToken=default)
        => _httpClient.GetFromJsonAsync<DocumentIntakeRuntimeSettings>("api/document-intake/operations/settings",cancellationToken);
    public async Task<Guid> QueueDocumentIntakePromptEvaluationAsync(QueuePromptEvaluationCommand command,CancellationToken cancellationToken=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/document-intake/operations/prompt-runs",command,cancellationToken);response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<DocumentIntakeIdResult>(cancellationToken:cancellationToken))!.Id;
    }
    public Task ApproveDocumentIntakePromptAsync(Guid promptId,ApproveDocumentIntakePromptCommand command,CancellationToken cancellationToken=default)
        => PostNoContentAsync($"api/document-intake/operations/prompts/{promptId}/approve",command,cancellationToken);

    public async Task<Guid> CreateDocumentIntakeAsync(CreateDocumentIntakeSessionCommand command,CancellationToken cancellationToken=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/document-intake",command,cancellationToken);response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<DocumentIntakeIdResult>(cancellationToken:cancellationToken))!.Id;
    }

    public Task AttachDocumentToIntakeAsync(Guid id,AttachDocumentToIntakeCommand command,CancellationToken cancellationToken=default)=>PostNoContentAsync($"api/document-intake/{id}/documents",command,cancellationToken);
    public Task QueueDocumentIntakeAsync(Guid id,QueueDocumentIntakeCommand command,CancellationToken cancellationToken=default)=>PostNoContentAsync($"api/document-intake/{id}/queue",command,cancellationToken);
    public Task ReviewDocumentIntakeFieldAsync(Guid id,Guid fieldId,ReviewDocumentIntakeFieldCommand command,CancellationToken cancellationToken=default)=>PutNoContentAsync($"api/document-intake/{id}/fields/{fieldId}/review",command,cancellationToken);
    public Task ResolveDocumentIntakeIssueAsync(Guid id,Guid issueId,ResolveDocumentIntakeIssueCommand command,CancellationToken cancellationToken=default)=>PutNoContentAsync($"api/document-intake/{id}/issues/{issueId}/resolve",command,cancellationToken);
    public Task ReprocessDocumentIntakeAsync(Guid id,ReprocessDocumentIntakeCommand command,CancellationToken cancellationToken=default)=>PostNoContentAsync($"api/document-intake/{id}/reprocess",command,cancellationToken);
    public Task CancelDocumentIntakeAsync(Guid id,CancelDocumentIntakeCommand command,CancellationToken cancellationToken=default)=>PostNoContentAsync($"api/document-intake/{id}/cancel",command,cancellationToken);

    public async Task<DocumentIntakePromotionResult?> PromoteDocumentIntakeAsync(Guid id,PromoteDocumentIntakeCommand command,CancellationToken cancellationToken=default)
    {
        using var response=await _httpClient.PostAsJsonAsync($"api/document-intake/{id}/promote",command,cancellationToken);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<DocumentIntakePromotionResult>(cancellationToken:cancellationToken);
    }

    private async Task PostNoContentAsync<T>(string uri,T command,CancellationToken cancellationToken){using var response=await _httpClient.PostAsJsonAsync(uri,command,cancellationToken);response.EnsureSuccessStatusCode();}
    private async Task PutNoContentAsync<T>(string uri,T command,CancellationToken cancellationToken){using var response=await _httpClient.PutAsJsonAsync(uri,command,cancellationToken);response.EnsureSuccessStatusCode();}
    private sealed record DocumentIntakeIdResult(Guid Id);
}
