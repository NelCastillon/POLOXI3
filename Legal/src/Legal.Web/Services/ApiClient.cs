using System.Net.Http.Json;
using System.Net;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.AspNetCore.Components.Forms;

namespace Legal.Web.Services;

// Typed HTTP client for the Legal.Api host. Only the endpoints used by the Legal
// search and configuration pages are exposed.
public sealed class ApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient=httpClient;

    // ── POLOXI Wide search (start+poll transport) ─────────────────────────────
    public async Task<WideSearchResponse?> IntelligentSearchWideDynamicAsync(WideSearchRequest request,CancellationToken token=default)
    {
        using var startResponse=await _httpClient.PostAsJsonAsync("api/intelligence_wide/search/dynamic/start",request,token);
        startResponse.EnsureSuccessStatusCode();
        var start=await startResponse.Content.ReadFromJsonAsync<WideSearchOperationStartResponse>(cancellationToken:token)??throw new InvalidOperationException("The wide search operation could not be started.");
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2),token);
                var status=await _httpClient.GetFromJsonAsync<WideSearchOperationStatusResponse>($"api/intelligence_wide/search/dynamic/status/{start.OperationId}",token)??throw new InvalidOperationException("The wide search operation is no longer available.");
                if(status.StatusCode=="COMPLETED")return status.Response??throw new InvalidOperationException("The wide search completed without a result.");
                if(status.StatusCode=="CANCELLED")throw new OperationCanceledException("The wide search was cancelled.");
                if(status.StatusCode=="FAILED")throw new InvalidOperationException(status.ErrorMessage??"The wide search failed.");
                if(status.StatusCode!="RUNNING")throw new InvalidOperationException($"The wide search returned an unexpected status: {status.StatusCode}.");
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested)
        {
            using var stopTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var stop=await _httpClient.PostAsync($"api/intelligence_wide/search/dynamic/cancel/{start.OperationId}",null,stopTimeout.Token);
                await EnsureSuccessWithDetailAsync(stop,stopTimeout.Token);
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException($"Stopped waiting for results, but server cancellation could not be confirmed: {ex.Message}",ex);
            }
            throw;
        }
    }

    public async Task<IReadOnlyCollection<WideModelOptionDto>> GetIntelligenceWideModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideModelOptionDto>>("api/intelligence_wide/models",token)??[];

    public async Task<IReadOnlyCollection<WideSearchContextDto>> GetIntelligenceSearchContextsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideSearchContextDto>>("api/intelligence_wide/contexts",token)??[];

    // ── POLOXI Wide2 pipeline (isolated clone backing /legal/personalinjury_decision2) ──────────────
    // Same start+poll transport as the Wide search, but every call targets the isolated api/intelligence_wide2
    // controller/service so changes to /legal/search never affect /legal/personalinjury_decision2.
    public async Task<WideSearchResponse?> IntelligentSearchWide2DynamicAsync(WideSearchRequest request,CancellationToken token=default)
    {
        using var startResponse=await _httpClient.PostAsJsonAsync("api/intelligence_wide2/search/dynamic/start",request,token);
        startResponse.EnsureSuccessStatusCode();
        var start=await startResponse.Content.ReadFromJsonAsync<WideSearchOperationStartResponse>(cancellationToken:token)??throw new InvalidOperationException("The wide search operation could not be started.");
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2),token);
                var status=await _httpClient.GetFromJsonAsync<WideSearchOperationStatusResponse>($"api/intelligence_wide2/search/dynamic/status/{start.OperationId}",token)??throw new InvalidOperationException("The wide search operation is no longer available.");
                if(status.StatusCode=="COMPLETED")return status.Response??throw new InvalidOperationException("The wide search completed without a result.");
                if(status.StatusCode=="CANCELLED")throw new OperationCanceledException("The wide search was cancelled.");
                if(status.StatusCode=="FAILED")throw new InvalidOperationException(status.ErrorMessage??"The wide search failed.");
                if(status.StatusCode!="RUNNING")throw new InvalidOperationException($"The wide search returned an unexpected status: {status.StatusCode}.");
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested)
        {
            using var stopTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var stop=await _httpClient.PostAsync($"api/intelligence_wide2/search/dynamic/cancel/{start.OperationId}",null,stopTimeout.Token);
                await EnsureSuccessWithDetailAsync(stop,stopTimeout.Token);
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException($"Stopped waiting for results, but server cancellation could not be confirmed: {ex.Message}",ex);
            }
            throw;
        }
    }

    public async Task<IReadOnlyCollection<WideModelOptionDto>> GetIntelligenceWide2ModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideModelOptionDto>>("api/intelligence_wide2/models",token)??[];

    public async Task<IReadOnlyCollection<WideSearchContextDto>> GetIntelligenceSearch2ContextsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WideSearchContextDto>>("api/intelligence_wide2/contexts",token)??[];

    public async Task<bool> GetSearch2ShowPipelineAsync(CancellationToken token=default)=>await TryGetShowPipelineAsync("api/intelligence_wide2/show-pipeline",token);

    // POLOXI Legal Decision Intelligence (/legal/decision) — self-contained module.
    public async Task<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse?> LegalDecideAsync(Legal.Application.Features.Intelligence.Decision.DecisionSearchRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/decide",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse>(cancellationToken:token);
    }

    public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionModelOptionDto>> GetLegalDecisionModelsAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionModelOptionDto>>("api/legal_decision/models",token)??[];

    public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContextDto>> GetLegalDecisionContextsAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContextDto>>("api/legal_decision/contexts",token)??[];

    // Configuration Mode admin surface (DB-backed execution settings per mode).
    public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionExecutionModeDto>> GetLegalDecisionExecutionModesAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionExecutionModeDto>>("api/legal_decision/execution-modes",token)??[];

    public async Task SaveLegalDecisionExecutionModeAsync(Legal.Application.Features.Intelligence.Decision.SaveDecisionExecutionModeRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/execution-modes/{request.ExecutionModeCode}",request,token);
        response.EnsureSuccessStatusCode();
    }

        // POLOXI Legal Decision cockpit — matter dashboard + timeline.
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>> GetLegalDecisionMattersAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>>("api/legal_decision/matters",token)??[];
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionMatterFacetsDto> GetLegalDecisionMatterFacetsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionMatterFacetsDto>("api/legal_decision/matters/facets",token)??new([],[],[]);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionDomainPackDto?> GetLegalDecisionDomainPackAsync(string packCode,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionDomainPackDto>($"api/legal_decision/domainpacks/{packCode}",token);
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionDomainPackDto>> GetLegalDecisionDomainPacksAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionDomainPackDto>>("api/legal_decision/domainpacks",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto?> GetLegalDecisionMatterAsync(Guid matterId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.DecisionMatterDto>($"api/legal_decision/matters/{matterId}",token);

        // ── Judz Matter Lifecycle (operational stage context) ──────────────────
        public Task<Legal.Application.Features.MatterLifecycle.MatterLifecycleSnapshotDto?> GetMatterLifecycleAsync(Guid matterId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.MatterLifecycle.MatterLifecycleSnapshotDto>($"api/legal_matter_lifecycle/{matterId}",token);
        public async Task<Legal.Application.Features.MatterLifecycle.MatterLifecycleSnapshotDto?> PerformMatterLifecycleTransitionAsync(Guid matterId,Legal.Application.Features.MatterLifecycle.PerformMatterLifecycleTransitionRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_matter_lifecycle/{matterId}/transitions",request,token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.MatterLifecycle.MatterLifecycleSnapshotDto>(cancellationToken:token);
        }

        // Judz Matter Lifecycle configuration (DB-backed authoring).
        public async Task<IReadOnlyCollection<Legal.Application.Features.MatterLifecycle.MatterLifecycleDefinitionDto>> GetMatterLifecycleDefinitionsAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.MatterLifecycle.MatterLifecycleDefinitionDto>>("api/legal_matter_lifecycle/config/definitions",token)??[];
        public Task<Legal.Application.Features.MatterLifecycle.MatterLifecycleDefinitionDetailDto?> GetMatterLifecycleDefinitionDetailAsync(Guid definitionId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.MatterLifecycle.MatterLifecycleDefinitionDetailDto>($"api/legal_matter_lifecycle/config/definitions/{definitionId}",token);
        public async Task<Guid> SaveMatterLifecycleDefinitionAsync(Legal.Application.Features.MatterLifecycle.SaveMatterLifecycleDefinitionRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_matter_lifecycle/config/definitions",request,token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<Guid> SaveMatterLifecycleStageAsync(Legal.Application.Features.MatterLifecycle.SaveMatterLifecycleStageRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_matter_lifecycle/config/stages",request,token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<Guid> SaveMatterLifecycleTransitionAsync(Legal.Application.Features.MatterLifecycle.SaveMatterLifecycleTransitionRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_matter_lifecycle/config/transitions",request,token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<Guid> CreateLegalDecisionMatterAsync(Legal.Application.Features.Intelligence.Decision.DecisionMatterCreateRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/matters",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
            var created=await response.Content.ReadFromJsonAsync<CreatedMatterResult>(cancellationToken:token);
            return created?.DecisionMatterId??Guid.Empty;
        }
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionTimelineEventDto>> GetLegalDecisionTimelineAsync(Guid sessionId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionTimelineEventDto>>($"api/legal_decision/sessions/{sessionId}/timeline",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse?> GetLegalDecisionSessionAsync(Guid sessionId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse>($"api/legal_decision/sessions/{sessionId}",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionClosedLoopResultDto?> ApplyLegalDecisionVerificationAsync(Guid sessionId,Legal.Application.Features.Intelligence.Decision.DecisionVerificationChangeRequest request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/sessions/{sessionId}/verify",request,token);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionClosedLoopResultDto>(token);}
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSessionSummaryDto>> GetLegalDecisionMatterSessionsAsync(Guid matterId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSessionSummaryDto>>($"api/legal_decision/matters/{matterId}/sessions",token)??[];
        public async Task<IReadOnlyCollection<LegalDocumentDto>> GetLegalMatterDocumentsAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<LegalDocumentDto>>($"api/legal_decision/matters/{matterId}/documents",token)??[];
        public async Task<Legal.Application.Features.Intelligence.Decision.LegalMatterEvidenceGraphDto?> GetLegalMatterEvidenceGraphAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.LegalMatterEvidenceGraphDto>($"api/legal_decision/matters/{matterId}/evidence-graph",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.CandidateFullAnalysisDto?> GetLegalCandidateFullAnalysisAsync(Guid matterId,int candidateIndex,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.CandidateFullAnalysisDto>($"api/legal_decision/matters/{matterId}/candidate-analysis/{candidateIndex}",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.MatterHumanIntelligenceDto?> GetLegalMatterHumanIntelligenceAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.MatterHumanIntelligenceDto>($"api/legal_decision/matters/{matterId}/human-intelligence",token);
        // ── Attorney Decision Input (Human Intelligence) write path ──────────────────────────────
        public async Task<Legal.Application.Features.Intelligence.Decision.AttorneyInputDraft?> CreateLegalAttorneyDraftAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.CreateAttorneyInputCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/decision-input/drafts",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.AttorneyInputDraft>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.PlacementAnalysis?> AnalyzeLegalAttorneyPlacementAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.AnalyzePlacementCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/decision-input/placement-analysis",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.PlacementAnalysis>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionMutationPreview?> PreviewLegalAttorneyInputAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.PreviewAttorneyInputCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/decision-input/preview",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionMutationPreview>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.CommitResult?> CommitLegalAttorneyInputAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.CommitAttorneyInputCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/decision-input/commit",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.CommitResult>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.ResolvedBranchNode?> ResolveLegalBranchNodeAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.ResolveBranchNodeCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/decision-input/resolve-branch-node",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.ResolvedBranchNode>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.AttorneyRelativeAssessmentDto?> SubmitLegalAttorneyAssessmentAsync(Guid matterId,Guid nodeId,Legal.Application.Features.Intelligence.Decision.SubmitAttorneyAssessmentCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/nodes/{nodeId}/attorney-assessments",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.AttorneyRelativeAssessmentDto>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.ApprovedMatterAssessmentDto?> ApproveLegalMatterAssessmentAsync(Guid matterId,Guid nodeId,Legal.Application.Features.Intelligence.Decision.ApproveMatterAssessmentCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/nodes/{nodeId}/assessment-approval",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.ApprovedMatterAssessmentDto>(cancellationToken:token);}

        // ── Document-Retrieval proposition review (Phase 2) ──────────────────────────────────────────
        // The attorney review queue + the accept / reject / revise / withdraw lifecycle, all routed through
        // the shared LPI integration funnel server-side. Responses carry the reassessment status so the UI
        // never presents the old ranking as current.
        public async Task<IReadOnlyList<Legal.Application.Abstractions.Intelligence.LpiReviewItemView>> GetRetrievedPropositionReviewAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyList<Legal.Application.Abstractions.Intelligence.LpiReviewItemView>>($"api/legal_decision/matters/{matterId}/propositions/review",token)??[];

        public async Task<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult?> AcceptRetrievedPropositionAsync(Guid matterId,Guid propositionId,object request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/propositions/review/{propositionId}/accept",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult>(cancellationToken:token);}

        public async Task RejectRetrievedPropositionAsync(Guid matterId,Guid propositionId,object request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/propositions/review/{propositionId}/reject",request,token);await EnsureSuccessWithDetailAsync(response,token);}

        public async Task<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult?> ReviseRetrievedPropositionAsync(Guid matterId,Guid propositionId,object request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/propositions/review/{propositionId}/revise",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult>(cancellationToken:token);}

        public async Task<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult?> WithdrawRetrievedPropositionAsync(Guid matterId,Guid propositionId,object request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/propositions/review/{propositionId}/withdraw",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.Lpi.LpiIntegrationResult>(cancellationToken:token);}

        public async Task<Legal.Application.Abstractions.Intelligence.RetrievalOrchestrationResult?> RunRetrievalPassAsync(Guid matterId,object request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/propositions/retrieval/run",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Abstractions.Intelligence.RetrievalOrchestrationResult>(cancellationToken:token);}
        public async Task RaiseLegalAttorneyChallengeAsync(Guid matterId,Guid nodeId,Legal.Application.Features.Intelligence.Decision.RaiseChallengeCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/nodes/{nodeId}/challenge",command,token);await EnsureSuccessWithDetailAsync(response,token);}
        public async Task<Legal.Application.Features.Intelligence.Decision.CommitResult?> RepositionLegalAttorneyNodeAsync(Guid matterId,Guid nodeId,Legal.Application.Features.Intelligence.Decision.RepositionNodeCommand command,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/nodes/{nodeId}/reposition",command,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.CommitResult>(cancellationToken:token);}
        public async Task<Legal.Application.Features.Intelligence.Epistemic.MatterPropositionInformationValueResult?> GetLegalMatterPropositionInformationValueAsync(Guid matterId,Guid sessionId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Epistemic.MatterPropositionInformationValueResult>($"api/legal_decision/matters/{matterId}/sessions/{sessionId}/proposition-information-value",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.NextBestActionResult?> GetLegalMatterNextBestActionsAsync(Guid matterId,Guid sessionId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.NextBestActionResult>($"api/legal_decision/matters/{matterId}/sessions/{sessionId}/next-best-actions",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.WhatToResolveNextResult?> GetLegalMatterWhatToResolveNextAsync(Guid matterId,Guid sessionId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.WhatToResolveNextResult>($"api/legal_decision/matters/{matterId}/sessions/{sessionId}/what-to-resolve-next",token);
        public async Task GenerateLegalMatterTestCorpusAsync(Guid matterId,CancellationToken token=default){using var response=await _httpClient.PostAsync($"api/legal_decision/matters/{matterId}/generate-test-corpus",null,token);await EnsureSuccessWithDetailAsync(response,token);}
        public async Task<LegalDocumentDto?> UploadLegalMatterDocumentAsync(Guid matterId,IBrowserFile file,string? documentTypeCode,string? domainPackCode,string? modelCode=null,CancellationToken token=default,string? idempotencyKey=null,Guid? uploadBatchId=null,EvidenceSourceDescriptor? source=null)
        {
            using var form=new MultipartFormDataContent();
            using var stream=file.OpenReadStream(100*1024*1024,token);
            using var content=new StreamContent(stream);
            content.Headers.ContentType=new(file.ContentType);
            form.Add(content,"file",file.Name);
            if(!string.IsNullOrWhiteSpace(documentTypeCode))form.Add(new StringContent(documentTypeCode),"documentTypeCode");
            if(!string.IsNullOrWhiteSpace(domainPackCode))form.Add(new StringContent(domainPackCode),"domainPackCode");
            if(!string.IsNullOrWhiteSpace(modelCode))form.Add(new StringContent(modelCode),"modelCode");
            if(!string.IsNullOrWhiteSpace(idempotencyKey))form.Add(new StringContent(idempotencyKey),"idempotencyKey");
            if(uploadBatchId is {} batchId)form.Add(new StringContent(batchId.ToString()),"uploadBatchId");
            if(source is not null)
            {
                if(!string.IsNullOrWhiteSpace(source.SourceTypeCode))form.Add(new StringContent(source.SourceTypeCode),"sourceTypeCode");
                if(!string.IsNullOrWhiteSpace(source.Custodian))form.Add(new StringContent(source.Custodian),"custodian");
                if(!string.IsNullOrWhiteSpace(source.ProducedBy))form.Add(new StringContent(source.ProducedBy),"producedBy");
                if(!string.IsNullOrWhiteSpace(source.ProductionId))form.Add(new StringContent(source.ProductionId),"productionId");
                if(!string.IsNullOrWhiteSpace(source.BatesStart))form.Add(new StringContent(source.BatesStart),"batesStart");
                if(!string.IsNullOrWhiteSpace(source.BatesEnd))form.Add(new StringContent(source.BatesEnd),"batesEnd");
            }
            using var response=await _httpClient.PostAsync($"api/legal_decision/matters/{matterId}/documents",form,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<LegalDocumentDto>(cancellationToken:token);
        }

        // Server-side byte[] variant used by integrations (e.g. Clio import) that already hold the
        // document content in memory rather than an IBrowserFile. Posts to the same intake endpoint.
        public async Task<LegalDocumentDto?> UploadLegalMatterDocumentBytesAsync(Guid matterId,byte[] content,string fileName,string contentType,string? documentTypeCode=null,string? domainPackCode=null,string? modelCode=null,string? idempotencyKey=null,Guid? uploadBatchId=null,EvidenceSourceDescriptor? source=null,CancellationToken token=default)
        {
            using var form=new MultipartFormDataContent();
            using var byteContent=new ByteArrayContent(content);
            byteContent.Headers.ContentType=new(string.IsNullOrWhiteSpace(contentType)?"application/octet-stream":contentType);
            form.Add(byteContent,"file",fileName);
            if(!string.IsNullOrWhiteSpace(documentTypeCode))form.Add(new StringContent(documentTypeCode),"documentTypeCode");
            if(!string.IsNullOrWhiteSpace(domainPackCode))form.Add(new StringContent(domainPackCode),"domainPackCode");
            if(!string.IsNullOrWhiteSpace(modelCode))form.Add(new StringContent(modelCode),"modelCode");
            if(!string.IsNullOrWhiteSpace(idempotencyKey))form.Add(new StringContent(idempotencyKey),"idempotencyKey");
            if(uploadBatchId is {} batchId)form.Add(new StringContent(batchId.ToString()),"uploadBatchId");
            if(source is not null)
            {
                if(!string.IsNullOrWhiteSpace(source.SourceTypeCode))form.Add(new StringContent(source.SourceTypeCode),"sourceTypeCode");
                if(!string.IsNullOrWhiteSpace(source.Custodian))form.Add(new StringContent(source.Custodian),"custodian");
                if(!string.IsNullOrWhiteSpace(source.ProducedBy))form.Add(new StringContent(source.ProducedBy),"producedBy");
                if(!string.IsNullOrWhiteSpace(source.ProductionId))form.Add(new StringContent(source.ProductionId),"productionId");
                if(!string.IsNullOrWhiteSpace(source.BatesStart))form.Add(new StringContent(source.BatesStart),"batesStart");
                if(!string.IsNullOrWhiteSpace(source.BatesEnd))form.Add(new StringContent(source.BatesEnd),"batesEnd");
            }
            using var response=await _httpClient.PostAsync($"api/legal_decision/matters/{matterId}/documents",form,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<LegalDocumentDto>(cancellationToken:token);
        }
        // ── Enterprise evidence-upload provenance layer ────────────────────────────────────────────────
        public async Task<Guid> StartLegalUploadBatchAsync(Guid matterId,StartUploadBatchCommand command,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/upload-batches",command,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<IReadOnlyCollection<LegalUploadBatchDto>> GetLegalUploadBatchesAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<LegalUploadBatchDto>>($"api/legal_decision/matters/{matterId}/upload-batches",token)??[];
        public async Task<IReadOnlyCollection<LegalEvidenceOccurrenceDto>> GetLegalEvidenceOccurrencesAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<LegalEvidenceOccurrenceDto>>($"api/legal_decision/matters/{matterId}/evidence-occurrences",token)??[];
        public async Task SetLegalUploadBatchDiscoveredAsync(Guid matterId,Guid batchId,int filesDiscovered,CancellationToken token=default)
        {
            using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/matters/{matterId}/upload-batches/{batchId}/discovered",new{FilesDiscovered=filesDiscovered},token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task CloseLegalUploadBatchAsync(Guid matterId,Guid batchId,string? statusCode=null,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/upload-batches/{batchId}/close",new{StatusCode=statusCode},token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task<IReadOnlyCollection<LegalEvidenceLineageGroupDto>> GetLegalEvidenceLineageAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<LegalEvidenceLineageGroupDto>>($"api/legal_decision/matters/{matterId}/evidence-lineage",token)??[];
        public async Task<Guid> CreateLegalEvidenceLineageGroupAsync(Guid matterId,string? lineageLabel,string? originDescription,string? independenceBasisCode,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/evidence-lineage",new{LineageLabel=lineageLabel,OriginDescription=originDescription,IndependenceBasisCode=independenceBasisCode},token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<Guid> AddLegalEvidenceLineageMemberAsync(Guid matterId,Guid groupId,Guid occurrenceId,string? roleCode,string? derivationNote,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/evidence-lineage/{groupId}/members",new{OccurrenceId=occurrenceId,RoleCode=roleCode,DerivationNote=derivationNote},token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task<Guid> CreateLegalDocumentFamilyAsync(Guid matterId,string? familyLabel,string? containerTypeCode,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/document-families",new{FamilyLabel=familyLabel,ContainerTypeCode=containerTypeCode},token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken:token);
        }
        public async Task LinkLegalOccurrenceToFamilyAsync(Guid matterId,Guid familyId,Guid occurrenceId,Guid? parentOccurrenceId,int familyDepth,int familyOrdinal,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/document-families/{familyId}/members",new{OccurrenceId=occurrenceId,ParentOccurrenceId=parentOccurrenceId,FamilyDepth=familyDepth,FamilyOrdinal=familyOrdinal},token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetLegalDocumentPassagesAsync(Guid documentVersionId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<LegalDocumentPassageDto>>($"api/legal_decision/documents/versions/{documentVersionId}/passages",token)??[];
        public async Task<Legal.Application.Features.Intelligence.Decision.LegalMatterCorpusActivationStatus?> GetLegalMatterCorpusStatusAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.LegalMatterCorpusActivationStatus>($"api/legal_decision/matters/{matterId}/corpus/enrichment-status",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.LegalMatterCorpusActivationStatus?> ActivateLegalMatterCorpusAsync(Guid matterId,string? modelCode=null,int batchSize=3,CancellationToken token=default)
        {
            var query=$"api/legal_decision/matters/{matterId}/corpus/activate?batchSize={batchSize}";
            if(!string.IsNullOrWhiteSpace(modelCode))query+=$"&modelCode={Uri.EscapeDataString(modelCode)}";
            using var response=await _httpClient.PostAsync(query,null,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.LegalMatterCorpusActivationStatus>(cancellationToken:token);
        }
        public async Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetLegalMatterRetrievalTelemetryAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<DecisionRetrievalTelemetryDto>>($"api/legal_decision/matters/{matterId}/retrieval-telemetry",token)??[];
        // Hard-deletes document-derived evidence (single/multiple documents when documentIds is provided,
        // or ALL supporting documents when null/empty) then synchronously recomputes all associated scoring
        // and decision statuses. Returns the purge counts plus the recomputed corpus activation status.
        public async Task<Legal.Application.Features.Intelligence.Decision.LegalMatterDocumentPurgeOutcome?> PurgeLegalMatterDocumentsAsync(Guid matterId,IReadOnlyCollection<Guid>? documentIds,CancellationToken token=default)
        {
            using var request=new HttpRequestMessage(HttpMethod.Delete,$"api/legal_decision/matters/{matterId}/corpus/documents")
            {
                Content=JsonContent.Create(new{DocumentIds=documentIds})
            };
            using var response=await _httpClient.SendAsync(request,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.LegalMatterDocumentPurgeOutcome>(cancellationToken:token);
        }
        public async Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetLegalSessionRetrievalTelemetryAsync(Guid sessionId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<DecisionRetrievalTelemetryDto>>($"api/legal_decision/sessions/{sessionId}/retrieval-telemetry",token)??[];
        // ── Decision Contract (first-class, versioned problem specification) ──────────────────────────
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> GetLegalDecisionContractAsync(Guid matterId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto>($"api/legal_decision/matters/{matterId}/decision-contract",token);
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContractOptionDto>> GetLegalDecisionContractOptionsAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionContractOptionDto>>("api/legal_decision/decision-contract/options",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractDecisionAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractDecisionCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/decision",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractLegalContextAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractLegalContextCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/legal-context",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractBurdenAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractBurdenCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/burden",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractBoundariesAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractBoundariesCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/boundaries",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractCandidatesAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractCandidatesCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/candidates",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> UpdateLegalDecisionContractSettingsAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractSettingsCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Put,$"api/legal_decision/matters/{matterId}/decision-contract/settings",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> SubmitLegalDecisionContractAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractLifecycleCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Post,$"api/legal_decision/matters/{matterId}/decision-contract/submit",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> ApproveLegalDecisionContractAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractLifecycleCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Post,$"api/legal_decision/matters/{matterId}/decision-contract/approve",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> ReturnLegalDecisionContractAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractLifecycleCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Post,$"api/legal_decision/matters/{matterId}/decision-contract/return",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> ActivateLegalDecisionContractAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionContractLifecycleCommand command,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Post,$"api/legal_decision/matters/{matterId}/decision-contract/activate",command,token);
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> CreateLegalDecisionContractVersionAsync(Guid matterId,Guid decisionContractId,CancellationToken token=default)=>SendDecisionContractAsync(HttpMethod.Post,$"api/legal_decision/matters/{matterId}/decision-contract/{decisionContractId}/new-version",new{},token);
        private async Task<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto?> SendDecisionContractAsync(HttpMethod method,string url,object body,CancellationToken token)
        {
            using var request=new HttpRequestMessage(method,url){Content=JsonContent.Create(body)};
            using var response=await _httpClient.SendAsync(request,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionContractWorkspaceDto>(cancellationToken:token);
        }
        // ── POLOXI Hierarchy Execution Lineage & Authority (schema 0366) ─────────────────────────────
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.HierarchyExecutionSummaryDto>> GetLegalHierarchyRunsAsync(Guid matterId,Guid contractId,int contractVersion,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.HierarchyExecutionSummaryDto>>($"api/legal_decision/hierarchy/runs?matterId={matterId}&contractId={contractId}&contractVersion={contractVersion}",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.HierarchyAuthorityDto?> GetLegalHierarchyAuthorityAsync(Guid matterId,Guid contractId,int contractVersion,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.HierarchyAuthorityDto>($"api/legal_decision/hierarchy/authority?matterId={matterId}&contractId={contractId}&contractVersion={contractVersion}",token);
        public Task<Legal.Application.Features.Intelligence.Decision.HierarchyExecutionDetailDto?> GetLegalHierarchyExecutionAsync(Guid hierarchyExecutionId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.HierarchyExecutionDetailDto>($"api/legal_decision/hierarchy/executions/{hierarchyExecutionId}",token);
        public async Task<Legal.Application.Features.Intelligence.Decision.PromoteHierarchyAuthorityResult?> PromoteLegalHierarchyExecutionAsync(Guid hierarchyExecutionId,byte[] rowVersion,string authorityReasonCode,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/hierarchy/executions/{hierarchyExecutionId}/promote",new{RowVersion=rowVersion,AuthorityReasonCode=authorityReasonCode},token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.PromoteHierarchyAuthorityResult>(cancellationToken:token);
        }
        // ── Continuous Decision Integrity — Decision Change Review workspace ─────────────────────────
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.MatterChangeReviewSummaryDto>> GetLegalDecisionChangeReviewsAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.MatterChangeReviewSummaryDto>>("api/legal_decision/change-reviews",token)??[];
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSnapshotDto>> GetLegalDecisionMatterSnapshotsAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionSnapshotDto>>($"api/legal_decision/matters/{matterId}/snapshots",token)??[];
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.MatterChangeEventDto>> GetLegalDecisionMatterChangeEventsAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.MatterChangeEventDto>>($"api/legal_decision/matters/{matterId}/change-events",token)??[];
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionReviewTaskDto>> GetLegalDecisionMatterReviewTasksAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionReviewTaskDto>>($"api/legal_decision/matters/{matterId}/review-tasks",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionChangeReviewDto?> GetLegalDecisionChangeReviewAsync(Guid changeEventId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.DecisionChangeReviewDto>($"api/legal_decision/change-events/{changeEventId}",token);
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionDeltaDto>> GetLegalDecisionMatterDeltasAsync(Guid matterId,CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.DecisionDeltaDto>>($"api/legal_decision/matters/{matterId}/deltas",token)??[];
        public Task<Legal.Application.Features.Intelligence.Decision.DecisionDeltaDto?> GetLegalDecisionChangeEventDeltaAsync(Guid changeEventId,CancellationToken token=default)=>GetFromJsonWithTransientThrottleRetryAsync<Legal.Application.Features.Intelligence.Decision.DecisionDeltaDto>($"api/legal_decision/change-events/{changeEventId}/delta",token);
        public async Task UpdateLegalDecisionReviewTaskStatusAsync(Guid reviewTaskId,Legal.Application.Features.Intelligence.Decision.UpdateReviewTaskStatusRequest request,CancellationToken token=default){using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/review-tasks/{reviewTaskId}/status",request,token);await EnsureSuccessWithDetailAsync(response,token);}
        public async Task UpdateLegalDecisionMatterAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.DecisionMatterUpdateRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/matters/{matterId}",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task UpdateLegalDecisionMatterStatusAsync(Guid matterId,string statusCode,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync($"api/legal_decision/matters/{matterId}/status",new Legal.Application.Features.Intelligence.Decision.DecisionMatterStatusUpdateRequest(statusCode),token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        public async Task DeleteLegalDecisionMatterAsync(Guid matterId,CancellationToken token=default)
        {
            using var response=await _httpClient.DeleteAsync($"api/legal_decision/matters/{matterId}",token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        // POLOXI Legal Decision — Personal Injury manual matter wizard (profile + child aggregates).
        public async Task<Legal.Application.Features.Intelligence.Decision.PersonalInjuryOptionsDto> GetLegalDecisionPersonalInjuryOptionsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.PersonalInjuryOptionsDto>("api/legal_decision/personalinjury/options",token)??new();
        public Task<Legal.Application.Features.Intelligence.Decision.PersonalInjuryProfileDto?> GetLegalDecisionPersonalInjuryProfileAsync(Guid matterId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.PersonalInjuryProfileDto>($"api/legal_decision/matters/{matterId}/personalinjury",token);
        public async Task SaveLegalDecisionPersonalInjuryProfileAsync(Guid matterId,Legal.Application.Features.Intelligence.Decision.PersonalInjuryProfileSaveRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PutAsJsonAsync($"api/legal_decision/matters/{matterId}/personalinjury",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        // POLOXI Legal Decision — Personal Injury Generate-New-Matter draft / provenance (scaffold).
        public async Task<Guid> CreateLegalDecisionPersonalInjuryDraftAsync(Legal.Application.Features.Intelligence.Decision.PersonalInjuryMatterDraftCreateRequest request,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/personalinjury/drafts",request,token);
            await EnsureSuccessWithDetailAsync(response,token);
            var created=await response.Content.ReadFromJsonAsync<CreatedDraftResult>(cancellationToken:token);
            return created?.DecisionPIMatterDraftId??Guid.Empty;
        }
        public Task<Legal.Application.Features.Intelligence.Decision.PersonalInjuryMatterDraftDto?> GetLegalDecisionPersonalInjuryDraftAsync(Guid draftId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Intelligence.Decision.PersonalInjuryMatterDraftDto>($"api/legal_decision/personalinjury/drafts/{draftId}",token);
        public async Task ConfirmLegalDecisionPersonalInjuryDraftAsync(Guid draftId,Guid matterId,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsync($"api/legal_decision/personalinjury/drafts/{draftId}/confirm/{matterId}",content:null,token);
            await EnsureSuccessWithDetailAsync(response,token);
        }
        // POLOXI Legal Decision — Personal Injury decision intelligence (decision types + PI decide pipeline).
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.PersonalInjuryDecisionTypeDto>> GetLegalDecisionPersonalInjuryDecisionTypesAsync(CancellationToken token=default)=>await GetFromJsonWithTransientThrottleRetryAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.PersonalInjuryDecisionTypeDto>>("api/legal_decision/personalinjury/decisiontypes",token)??[];
        public async Task<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.PersonalInjuryStageDecisionDto>> GetLegalDecisionPersonalInjuryStageDecisionMapAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.Intelligence.Decision.PersonalInjuryStageDecisionDto>>("api/legal_decision/personalinjury/stagedecisionmap",token)??[];
        public async Task<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse?> DecideLegalDecisionPersonalInjuryAsync(Legal.Application.Features.Intelligence.Decision.PersonalInjuryDecisionContext context,CancellationToken token=default)
        {
            using var response=await _httpClient.PostAsJsonAsync("api/legal_decision/personalinjury/decide",context,token);
            await EnsureSuccessWithDetailAsync(response,token);
            return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Decision.DecisionSearchResponse>(cancellationToken:token);
        }

        private sealed record CreatedDraftResult(Guid DecisionPIMatterDraftId);

        private sealed record CreatedMatterResult(Guid DecisionMatterId);

    public async Task<IReadOnlyList<MathExecutionSummary>> GetMathRunsAsync(int take=50,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<MathExecutionSummary>>($"api/intelligence_math/runs?take={take}",token)??[];
    public Task<MathExecutionDetail?> GetMathRunAsync(Guid mathExecutionId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<MathExecutionDetail>($"api/intelligence_math/runs/{mathExecutionId}",token);

    // ── Enterprise Error Log (Administrator → Error Logs) ──────────────────────
    public async Task<IReadOnlyList<Legal.Application.Abstractions.Services.ErrorLogListItem>> GetErrorLogsAsync(int days=7,int take=200,string? module=null,string? severity=null,string? search=null,bool allTenants=false,CancellationToken token=default)
    {
        var query=$"api/error_logs?days={days}&take={take}&allTenants={allTenants.ToString().ToLowerInvariant()}";
        if(!string.IsNullOrWhiteSpace(module))query+=$"&module={Uri.EscapeDataString(module)}";
        if(!string.IsNullOrWhiteSpace(severity))query+=$"&severity={Uri.EscapeDataString(severity)}";
        if(!string.IsNullOrWhiteSpace(search))query+=$"&search={Uri.EscapeDataString(search)}";
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Abstractions.Services.ErrorLogListItem>>(query,token)??[];
    }

    // ── POLOXI Math solve (deterministic verification pipeline) ──────────────
    public async Task<Legal.Application.Features.Intelligence.Science.MathSolveResponse?> SolveMathAsync(Legal.Application.Features.Intelligence.Science.MathSolveRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/intelligence_math/solve",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Science.MathSolveResponse>(cancellationToken:token);
    }

    // ── POLOXI Formalization Gate (Research → Formalize → Math handoff) ──────────
    public async Task<Legal.Application.Features.Intelligence.Science.FormalizationResponse?> FormalizeAsync(Legal.Application.Features.Intelligence.Science.FormalizationRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/intelligence_formalization/formalize",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Intelligence.Science.FormalizationResponse>(cancellationToken:token);
    }

    // ── Owner-only Account Settings (organization/tenant profile)
    public Task<Legal.Application.Features.Saas.TenantProfileDto?> GetTenantProfileAsync(CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.TenantProfileDto>("api/account_settings/profile",token);
    public async Task<Legal.Application.Features.Saas.TenantProfileDto?> UpdateTenantProfileAsync(Legal.Application.Features.Saas.UpdateTenantProfileRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync("api/account_settings/profile",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.TenantProfileDto>(cancellationToken:token);}

    // ── Configuration center
    public Task<IntelligencePlatformSummaryDto?> GetIntelligencePlatformAsync(CancellationToken token=default)=>_httpClient.GetFromJsonAsync<IntelligencePlatformSummaryDto>("api/intelligence/platform",token);
    public async Task<IReadOnlyCollection<AiProviderDto>> GetIntelligenceProvidersAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiProviderDto>>("api/intelligence/providers",token)??[];
    public async Task SaveIntelligenceProviderAsync(string providerCode,SaveAiProviderRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/providers/{Uri.EscapeDataString(providerCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceProviderAsync(string providerCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/providers/{Uri.EscapeDataString(providerCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<AiModelDeploymentDto>> GetIntelligenceModelsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiModelDeploymentDto>>("api/intelligence/models",token)??[];
    public async Task SaveIntelligenceModelDeploymentAsync(string modelCode,SaveAiModelDeploymentRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/models/{Uri.EscapeDataString(modelCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceModelDeploymentAsync(string modelCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/models/{Uri.EscapeDataString(modelCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetIntelligenceFeaturePoliciesAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AiFeaturePolicyDto>>("api/intelligence/feature-policies",token)??[];
    public async Task SaveIntelligenceFeaturePolicyAsync(string featureCode,SaveAiFeaturePolicyRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/feature-policies/{Uri.EscapeDataString(featureCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligenceFeaturePolicyAsync(string featureCode,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/feature-policies/{Uri.EscapeDataString(featureCode)}",token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetIntelligencePromptDefinitionsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<IntelligencePromptDefinitionDto>>("api/intelligence/prompts",token)??[];
    public async Task SaveIntelligencePromptDefinitionAsync(string promptCode,string versionLabel,SaveIntelligencePromptDefinitionRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/prompts/{Uri.EscapeDataString(promptCode)}/{Uri.EscapeDataString(versionLabel)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<DecisionPromptConfigurationDto>> GetDecisionPromptConfigurationsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DecisionPromptConfigurationDto>>("api/intelligence/decision-prompts",token)??[];
    public async Task<IReadOnlyCollection<DecisionModelRouteDto>> GetDecisionModelRoutesAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DecisionModelRouteDto>>("api/intelligence/decision-model-routes",token)??[];
    public async Task SaveDecisionPromptConfigurationAsync(string promptCode,SaveDecisionPromptConfigurationRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/decision-prompts/{Uri.EscapeDataString(promptCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task SaveDecisionModelRouteAsync(string featureCode,SaveDecisionModelRouteRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/decision-model-routes/{Uri.EscapeDataString(featureCode)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task<IReadOnlyCollection<DecisionSettingDto>> GetDecisionSettingsAsync(string? keyPrefix=null,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<DecisionSettingDto>>($"api/intelligence/decision-settings{(string.IsNullOrWhiteSpace(keyPrefix)?string.Empty:$"?keyPrefix={Uri.EscapeDataString(keyPrefix)}")}",token)??[];
    public async Task SaveDecisionSettingAsync(string settingKey,SaveDecisionSettingRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/decision-settings/{Uri.EscapeDataString(settingKey)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteIntelligencePromptDefinitionAsync(string promptCode,string versionLabel,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/prompts/{Uri.EscapeDataString(promptCode)}/{Uri.EscapeDataString(versionLabel)}",token);await EnsureSuccessWithDetailAsync(response,token);}

    // Legal Grounding settings (CourtListener/GovInfo/eCFR) stored in Core.ConfigurationSetting.
    public async Task<IReadOnlyCollection<LegalGroundingSettingDto>> GetLegalGroundingSettingsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<LegalGroundingSettingDto>>("api/intelligence/legal-grounding-settings",token)??[];
    public async Task SaveLegalGroundingSettingAsync(string settingKey,SaveLegalGroundingSettingRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/legal-grounding-settings/{Uri.EscapeDataString(settingKey)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}
    public async Task DeleteLegalGroundingSettingAsync(string settingKey,CancellationToken token=default){var response=await _httpClient.DeleteAsync($"api/intelligence/legal-grounding-settings/{Uri.EscapeDataString(settingKey)}",token);await EnsureSuccessWithDetailAsync(response,token);}

    // Epistemic Authority (POLOXI EA) settings stored in Core.ConfigurationSetting (tenant override + platform default).
    public async Task<IReadOnlyCollection<EpistemicSettingDto>> GetEpistemicSettingsAsync(CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<EpistemicSettingDto>>("api/intelligence/epistemic-settings",token)??[];
    public async Task SaveEpistemicSettingAsync(string settingKey,SaveEpistemicSettingRequest request,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync($"api/intelligence/epistemic-settings/{Uri.EscapeDataString(settingKey)}",request,token);await EnsureSuccessWithDetailAsync(response,token);}

    // Search result display toggle for the End-to-end POLOXI pipeline section.
    public async Task<bool> GetShowPipelineAsync(CancellationToken token=default)=>await TryGetShowPipelineAsync("api/intelligence/show-pipeline",token);
    public async Task<bool> GetSearchShowPipelineAsync(CancellationToken token=default)=>await TryGetShowPipelineAsync("api/intelligence_wide/show-pipeline",token);
    private async Task<bool> TryGetShowPipelineAsync(string url,CancellationToken token)
    {
        using var response=await _httpClient.GetAsync(url,token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return false;
        await EnsureSuccessWithDetailAsync(response,token);
        return (await response.Content.ReadFromJsonAsync<ShowPipelineResponse>(token))?.ShowPipeline??false;
    }
    public async Task SaveShowPipelineAsync(bool showPipeline,CancellationToken token=default){var response=await _httpClient.PutAsJsonAsync("api/intelligence/show-pipeline",new{showPipeline},token);await EnsureSuccessWithDetailAsync(response,token);}

    private sealed record ShowPipelineResponse(bool ShowPipeline);

    // ── Judz.ai Early Access SaaS — public authentication surface ──────────────
    // Calls the anonymous api/auth/* endpoints. The API returns loosely-typed JSON
    // envelopes ({ message, errors, outcome, ... }); AuthResult normalises them.
    public Task<AuthResult> SignupAsync(Legal.Application.Features.Saas.SignupRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/signup",request,token);    public Task<AuthResult> VerifyEmailAsync(Legal.Application.Features.Saas.VerifyEmailRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/verify-email",request,token);

    public Task<AuthResult> ResendVerificationAsync(Legal.Application.Features.Saas.ResendVerificationRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/resend-verification",request,token);

    public Task<AuthResult> LoginAsync(Legal.Application.Features.Saas.LoginRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/login",request,token);

    public Task<AuthResult> ForgotPasswordAsync(Legal.Application.Features.Saas.ForgotPasswordRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/forgot-password",request,token);

    public Task<AuthResult> ResetPasswordAsync(Legal.Application.Features.Saas.ResetPasswordRequest request,CancellationToken token=default)
        =>PostAuthAsync("api/auth/reset-password",request,token);

    private async Task<AuthResult> PostAuthAsync(string url,object request,CancellationToken token)
    {
        using var response=await _httpClient.PostAsJsonAsync(url,request,token);
        AuthEnvelope? envelope=null;
        try{envelope=await response.Content.ReadFromJsonAsync<AuthEnvelope>(cancellationToken:token);}catch{/* non-JSON body */}
        if(response.IsSuccessStatusCode)
            return new AuthResult(true,envelope?.Message,null,envelope?.Outcome,envelope?.TenantId,envelope?.RequiresVerification??false,envelope?.UserId,envelope?.Email,envelope?.DisplayName,envelope?.Permissions??[],envelope?.RoleCode);

        var errors=envelope?.Errors is{Count:>0}?string.Join(" ",envelope.Errors):null;
        var message=envelope?.Message??errors??$"Request failed with status {(int)response.StatusCode}.";
        return new AuthResult(false,message,errors,envelope?.Outcome,envelope?.TenantId,envelope?.RequiresVerification??false,envelope?.UserId,envelope?.Email,envelope?.DisplayName,envelope?.Permissions??[],envelope?.RoleCode);
    }

    private sealed record AuthEnvelope(string? Message,List<string>? Errors,string? Outcome,Guid? TenantId,bool? RequiresVerification,Guid? UserId,string? Email,string? DisplayName,List<string>? Permissions,string? RoleCode);

    // Anonymous legal agreements for the signup clickwrap surface.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.LegalAgreementDto>>("api/auth/agreements",token)??[];

    // Tenant Configuration control plane (/admin/configuration).
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantConfigurationCategoryDto>> GetTenantConfigurationAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantConfigurationCategoryDto>>("api/tenant_configuration",token)??[];

    public async Task SetTenantConfigurationValueAsync(string key,string? valueJson,CancellationToken token=default)
    {
        var request=new Legal.Application.Features.Saas.SetTenantConfigurationRequest(key,valueJson);
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_configuration",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Platform Configuration control plane (/platform/configuration) — Super Admin only.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.PlatformConfigurationCategoryDto>> GetPlatformConfigurationAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.PlatformConfigurationCategoryDto>>("api/platform_configuration",token)??[];

    public async Task SetPlatformConfigurationValueAsync(string key,string? valueJson,CancellationToken token=default)
    {
        var request=new Legal.Application.Features.Saas.SetPlatformConfigurationRequest(key,valueJson);
        using var response=await _httpClient.PutAsJsonAsync("api/platform_configuration",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant User Management (/admin/users) — Tenant Admin, own tenant only.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>> GetTenantMembersAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>>("api/tenant_users",token)??[];

    public async Task<Legal.Application.Features.Saas.MemberPageDto> GetTenantMembersPageAsync(string? search=null,string? status=null,int page=1,int pageSize=25,CancellationToken token=default)
    {
        var query=$"api/tenant_users/page?page={page}&pageSize={pageSize}";
        if(!string.IsNullOrWhiteSpace(search))query+=$"&search={Uri.EscapeDataString(search.Trim())}";
        if(!string.IsNullOrWhiteSpace(status))query+=$"&status={Uri.EscapeDataString(status.Trim())}";
        return await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.MemberPageDto>(query,token)
            ??new Legal.Application.Features.Saas.MemberPageDto([],0,0,0,0,page,pageSize);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>> GetTenantAssignableRolesAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>>("api/tenant_users/roles",token)??[];

    // Legal clickwrap consent evidence for a member.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ConsentRecordDto>> GetTenantMemberConsentAsync(Guid userId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ConsentRecordDto>>($"api/tenant_users/{userId}/consent",token)??[];

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> InviteTenantMemberAsync(Legal.Application.Features.Saas.InviteMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.InviteMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/tenant_users/invite",request,token);

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> CreateTenantMemberAsync(Legal.Application.Features.Saas.CreateMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/tenant_users/create",request,token);

    public async Task ChangeTenantMemberRoleAsync(Legal.Application.Features.Saas.ChangeMemberRoleRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_users/role",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ChangeTenantMemberStatusAsync(Legal.Application.Features.Saas.ChangeMemberStatusRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_users/status",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemoveTenantMemberAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_users/{membershipId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ResetTenantMemberLockoutAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/{membershipId}/reset-lockout",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task SetTenantMemberPasswordAsync(Legal.Application.Features.Saas.SetMemberPasswordRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/tenant_users/set-password",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Invitations (Phase B) — Tenant Admin manages invitations for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantInvitationDto>> GetTenantInvitationsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantInvitationDto>>("api/tenant_users/invitations",token)??[];

    public async Task<Legal.Application.Features.Saas.TenantInvitationDto> CreateTenantInvitationAsync(Legal.Application.Features.Saas.CreateInvitationRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateInvitationRequest,Legal.Application.Features.Saas.TenantInvitationDto>("api/tenant_users/invitations",request,token);

    public async Task ResendTenantInvitationAsync(Guid invitationId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/invitations/{invitationId}/resend",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RevokeTenantInvitationAsync(Guid invitationId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_users/invitations/{invitationId}/revoke",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Groups (Phase C) — Tenant Admin manages groups for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantGroupDto>> GetTenantGroupsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantGroupDto>>("api/tenant_groups",token)??[];

    public async Task<Legal.Application.Features.Saas.TenantGroupDetailDto?> GetTenantGroupAsync(Guid groupId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.TenantGroupDetailDto>($"api/tenant_groups/{groupId}",token);

    public async Task<Legal.Application.Features.Saas.TenantGroupDto> CreateTenantGroupAsync(Legal.Application.Features.Saas.CreateGroupRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateGroupRequest,Legal.Application.Features.Saas.TenantGroupDto>("api/tenant_groups",request,token);

    public async Task UpdateTenantGroupAsync(Legal.Application.Features.Saas.UpdateGroupRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/tenant_groups",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task DeleteTenantGroupAsync(Guid groupId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_groups/{groupId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.GroupMemberDto>> GetTenantGroupMembersAsync(Guid groupId,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.GroupMemberDto>>($"api/tenant_groups/{groupId}/members",token)??[];

    public async Task AddTenantGroupMemberAsync(Guid groupId,Guid userId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/tenant_groups/{groupId}/members/{userId}",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemoveTenantGroupMemberAsync(Guid groupId,Guid userId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/tenant_groups/{groupId}/members/{userId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    // Tenant Activity (Phase C) — read-only audit/usage/login history for own tenant.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetActivityAuditEventsAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/audit?days={days}&take={take}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetActivityUsageAsync(int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/usage?days={days}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.LoginHistoryDto>> GetActivityLoginHistoryAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.LoginHistoryDto>>($"api/tenant_activity/logins?days={days}&take={take}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetMyAuditEventsAsync(int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/me/audit?days={days}&take={take}",token)??[];

    public Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetMyAuditEventsPageAsync(int days=30,string? search=null,int page=1,int pageSize=25,CancellationToken token=default)
        =>GetAuditPageAsync($"api/tenant_activity/me/audit/page?days={days}",search,page,pageSize,token);

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetMyUsageAsync(int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/me/usage?days={days}",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>> GetUserAuditEventsAsync(Guid userId,Guid? tenantId=null,int days=30,int take=100,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AuditEventDto>>($"api/tenant_activity/users/{userId}/audit?tenantId={tenantId}&days={days}&take={take}",token)??[];

    public Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetUserAuditEventsPageAsync(Guid userId,Guid? tenantId=null,int days=30,string? search=null,int page=1,int pageSize=25,CancellationToken token=default)
        =>GetAuditPageAsync($"api/tenant_activity/users/{userId}/audit/page?tenantId={tenantId}&days={days}",search,page,pageSize,token);

    private async Task<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>> GetAuditPageAsync(string endpoint,string? search,int page,int pageSize,CancellationToken token)
    {
        var query=$"{endpoint}&page={page}&pageSize={pageSize}";
        if(!string.IsNullOrWhiteSpace(search))query+=$"&search={Uri.EscapeDataString(search.Trim())}";
        return await _httpClient.GetFromJsonAsync<Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>>(query,token)
            ??new Legal.Application.Features.Saas.PagedResultDto<Legal.Application.Features.Saas.AuditEventDto>([],0,page,pageSize);
    }

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>> GetUserUsageAsync(Guid userId,Guid? tenantId=null,int days=30,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.UsageSummaryDto>>($"api/tenant_activity/users/{userId}/usage?tenantId={tenantId}&days={days}",token)??[];

    // Public invitation acceptance (/invitations/{token}) — anonymous, token-only.
    public async Task<Legal.Application.Features.Saas.InvitationLookupDto?> LookupInvitationAsync(string invitationToken,CancellationToken token=default)
    {
        using var response=await _httpClient.GetAsync($"api/invitations/{Uri.EscapeDataString(invitationToken)}",token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return null;
        await EnsureSuccessWithDetailAsync(response,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.InvitationLookupDto>(cancellationToken:token);
    }

    public async Task<Legal.Application.Features.Saas.AcceptInvitationResult?> AcceptInvitationAsync(Legal.Application.Features.Saas.AcceptInvitationRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/invitations/accept",request,token);
        return await response.Content.ReadFromJsonAsync<Legal.Application.Features.Saas.AcceptInvitationResult>(cancellationToken:token);
    }

    // Platform User Management (/platform/users) — Super Admin, all tenants.
    public async Task<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>> GetPlatformMembersAsync(Guid? tenantId=null,CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.ManagedMemberDto>>(tenantId.HasValue?$"api/platform_users?tenantId={tenantId.Value}":"api/platform_users",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>> GetPlatformAssignableRolesAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.AssignableRoleDto>>("api/platform_users/roles",token)??[];

    public async Task<IReadOnlyList<Legal.Application.Features.Saas.TenantOptionDto>> GetPlatformTenantsAsync(CancellationToken token=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<Legal.Application.Features.Saas.TenantOptionDto>>("api/platform_users/tenants",token)??[];

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> InvitePlatformMemberAsync(Legal.Application.Features.Saas.InviteMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.InviteMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/platform_users/invite",request,token);

    public async Task<Legal.Application.Features.Saas.ProvisionMemberResult> CreatePlatformMemberAsync(Legal.Application.Features.Saas.CreateMemberRequest request,CancellationToken token=default)
        =>await PostForResultAsync<Legal.Application.Features.Saas.CreateMemberRequest,Legal.Application.Features.Saas.ProvisionMemberResult>("api/platform_users/create",request,token);

    public async Task ChangePlatformMemberRoleAsync(Legal.Application.Features.Saas.ChangeMemberRoleRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/platform_users/role",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ChangePlatformMemberStatusAsync(Legal.Application.Features.Saas.ChangeMemberStatusRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PutAsJsonAsync("api/platform_users/status",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task RemovePlatformMemberAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.DeleteAsync($"api/platform_users/{membershipId}",token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task ResetPlatformMemberLockoutAsync(Guid membershipId,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsync($"api/platform_users/{membershipId}/reset-lockout",null,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    public async Task SetPlatformMemberPasswordAsync(Legal.Application.Features.Saas.SetMemberPasswordRequest request,CancellationToken token=default)
    {
        using var response=await _httpClient.PostAsJsonAsync("api/platform_users/set-password",request,token);
        await EnsureSuccessWithDetailAsync(response,token);
    }

    private async Task<TResult> PostForResultAsync<TRequest,TResult>(string uri,TRequest request,CancellationToken token)
    {
        using var response=await _httpClient.PostAsJsonAsync(uri,request,token);
        await EnsureSuccessWithDetailAsync(response,token);
        return (await response.Content.ReadFromJsonAsync<TResult>(token))!;
    }

    private async Task<TResult?> GetFromJsonWithTransientThrottleRetryAsync<TResult>(string uri,CancellationToken token)
    {
        const int maximumAttempts=3;
        for(var attempt=1;attempt<=maximumAttempts;attempt++)
        {
            using var response=await _httpClient.GetAsync(uri,token);
            if(response.StatusCode!=HttpStatusCode.TooManyRequests||attempt==maximumAttempts)
            {
                if(response.StatusCode==HttpStatusCode.TooManyRequests)
                {
                    var detail=await response.Content.ReadAsStringAsync(token);
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? $"The API throttled GET {uri} after {maximumAttempts} attempts."
                        : $"The API throttled GET {uri} after {maximumAttempts} attempts: {detail}");
                }
                await EnsureSuccessWithDetailAsync(response,token);
                // A successful response can legitimately carry no body (HTTP 204 No Content, or an empty
                // 200 when the server has nothing to return). ReadFromJsonAsync throws "The input does not
                // contain any JSON tokens" on an empty stream, so treat an empty body as a null result
                // (callers already coalesce null to [] / handle null) instead of surfacing a parse error.
                if(response.StatusCode==HttpStatusCode.NoContent||response.Content.Headers.ContentLength==0)return default;
                var payload=await response.Content.ReadAsStringAsync(token);
                if(string.IsNullOrWhiteSpace(payload))return default;
                return System.Text.Json.JsonSerializer.Deserialize<TResult>(payload,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            }

            var retryAfter=response.Headers.RetryAfter;
            var delay=retryAfter?.Delta
                ?? (retryAfter?.Date-DateTimeOffset.UtcNow)
                ?? TimeSpan.FromMilliseconds(250*Math.Pow(2,attempt-1));
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds,100,5000)),token);
        }
        return default;
    }

    private static async Task EnsureSuccessWithDetailAsync(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var detail=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)?$"Request failed with status {(int)response.StatusCode}.":detail);}

    // ── Provider portal: sharing policy, firm→provider requests, review state ──
    public async Task<IReadOnlyCollection<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto>> GetProviderSharingPoliciesAsync(Guid matterId,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto>>($"api/legal_provider_portal/matters/{matterId}/sharing-policies",token)??[];

    public Task<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto?> GetProviderSharingPolicyAsync(Guid matterId,string providerKey,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto>($"api/legal_provider_portal/matters/{matterId}/sharing-policies/{Uri.EscapeDataString(providerKey)}",token);

    public async Task<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto?> SaveProviderSharingPolicyAsync(Legal.Application.Features.ProviderPortal.SaveProviderSharingPolicyRequest request,CancellationToken token=default){using var response=await _httpClient.PutAsJsonAsync($"api/legal_provider_portal/matters/{request.MatterId}/sharing-policies",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.ProviderPortal.ProviderSharingPolicyDto>(cancellationToken:token);}

    public async Task<IReadOnlyCollection<Legal.Application.Features.ProviderPortal.ProviderRequestDto>> GetProviderRequestsAsync(Guid matterId,string? providerKey=null,CancellationToken token=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyCollection<Legal.Application.Features.ProviderPortal.ProviderRequestDto>>($"api/legal_provider_portal/matters/{matterId}/requests{(string.IsNullOrWhiteSpace(providerKey)?string.Empty:$"?providerKey={Uri.EscapeDataString(providerKey)}")}",token)??[];

    public async Task<Legal.Application.Features.ProviderPortal.ProviderRequestDto?> CreateProviderRequestAsync(Legal.Application.Features.ProviderPortal.CreateProviderRequestRequest request,CancellationToken token=default){using var response=await _httpClient.PostAsJsonAsync($"api/legal_provider_portal/matters/{request.MatterId}/requests",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.ProviderPortal.ProviderRequestDto>(cancellationToken:token);}

    public async Task<Legal.Application.Features.ProviderPortal.ProviderRequestDto?> UpdateProviderRequestStatusAsync(Legal.Application.Features.ProviderPortal.UpdateProviderRequestStatusRequest request,CancellationToken token=default){using var response=await _httpClient.PutAsJsonAsync($"api/legal_provider_portal/requests/{request.ProviderRequestId}/status",request,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.ProviderPortal.ProviderRequestDto>(cancellationToken:token);}

    public Task<Legal.Application.Features.ProviderPortal.MatterReviewStateDto?> GetMatterReviewStateAsync(Guid matterId,CancellationToken token=default)=>_httpClient.GetFromJsonAsync<Legal.Application.Features.ProviderPortal.MatterReviewStateDto>($"api/legal_provider_portal/matters/{matterId}/review-state",token);

    public async Task<Legal.Application.Features.ProviderPortal.MatterReviewStateDto?> RecordMatterOpenedAsync(Guid matterId,CancellationToken token=default){using var response=await _httpClient.PostAsync($"api/legal_provider_portal/matters/{matterId}/review-state/open",null,token);await EnsureSuccessWithDetailAsync(response,token);return await response.Content.ReadFromJsonAsync<Legal.Application.Features.ProviderPortal.MatterReviewStateDto>(cancellationToken:token);}
}

/// <summary>Normalised result of a Judz.ai auth endpoint call for the Blazor UI.</summary>
public sealed record AuthResult(
    bool Succeeded,
    string? Message,
    string? Errors,
    string? Outcome,
    Guid? TenantId,
    bool RequiresVerification,
    Guid? UserId = null,
    string? Email = null,
    string? DisplayName = null,
    IReadOnlyList<string>? Permissions = null,
    string? RoleCode = null);
