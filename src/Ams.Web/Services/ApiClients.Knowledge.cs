using System.Net;
using System.Net.Http.Json;
using Ams.Knowledge.Application.Common.Models;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Ams.Knowledge.Contracts.Validation;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<KnowledgeDashboardDto?> GetKnowledgeDashboardAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<KnowledgeDashboardDto>("api/knowledge/dashboard", cancellationToken);

    public Task<PagedResult<WorkflowGuideStepDto>?> SearchWorkflowGuideStepsAsync(string? searchTerm = null, string? moduleCode = null, string? stageName = null, bool includeOptional = true, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowGuideStepDto>>($"api/knowledge/workflow-guide?searchTerm={Encode(searchTerm)}&moduleCode={Encode(moduleCode)}&stageName={Encode(stageName)}&includeOptional={includeOptional}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<ConceptSchemeDto>?> SearchKnowledgeSchemesAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ConceptSchemeDto>>($"api/knowledge/schemes?searchTerm={Encode(searchTerm)}&statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateKnowledgeSchemeAsync(CreateConceptSchemeCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync("api/knowledge/schemes", command, cancellationToken);

    public Task UpdateKnowledgeSchemeAsync(Guid id, UpdateConceptSchemeCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/schemes/{id}", command, cancellationToken);

    public Task DeleteKnowledgeSchemeAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/schemes/{id}", command, cancellationToken);

    public Task<PagedResult<KnowledgeConceptDto>?> SearchKnowledgeConceptsAsync(Guid? conceptSchemeId = null, string? searchTerm = null, string? conceptTypeCode = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<KnowledgeConceptDto>>($"api/knowledge/concepts?conceptSchemeId={conceptSchemeId}&searchTerm={Encode(searchTerm)}&conceptTypeCode={Encode(conceptTypeCode)}&statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<KnowledgeConceptDto?> GetKnowledgeConceptAsync(Guid conceptId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/knowledge/concepts/{conceptId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<KnowledgeConceptDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateKnowledgeConceptAsync(CreateKnowledgeConceptCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync("api/knowledge/concepts", command, cancellationToken);

    public async Task UpdateKnowledgeConceptDraftAsync(Guid conceptId, UpdateKnowledgeConceptDraftCommand command, CancellationToken cancellationToken = default)
        => (await _httpClient.PutAsJsonAsync($"api/knowledge/concepts/{conceptId}", command, cancellationToken)).EnsureSuccessStatusCode();

    public Task DeleteKnowledgeConceptAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/concepts/{id}", command, cancellationToken);

    public async Task<IReadOnlyCollection<ConceptLabelDto>> GetKnowledgeLabelsAsync(Guid conceptId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<ConceptLabelDto>>($"api/knowledge/concepts/{conceptId}/labels", cancellationToken) ?? [];

    public async Task<Guid> AddKnowledgeLabelAsync(Guid conceptId, AddConceptLabelCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync($"api/knowledge/concepts/{conceptId}/labels", command, cancellationToken);

    public Task UpdateKnowledgeLabelAsync(Guid conceptId, Guid labelId, UpdateConceptLabelCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/concepts/{conceptId}/labels/{labelId}", command, cancellationToken);

    public Task DeleteKnowledgeLabelAsync(Guid conceptId, Guid labelId, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/concepts/{conceptId}/labels/{labelId}", command, cancellationToken);

    public async Task<Guid> AddKnowledgeRelationshipAsync(AddConceptRelationshipCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync("api/knowledge/concepts/relationships", command, cancellationToken);

    public async Task<IReadOnlyCollection<ConceptRelationshipDto>> GetKnowledgeRelationshipsAsync(Guid? conceptId = null, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<ConceptRelationshipDto>>($"api/knowledge/concepts/relationships?conceptId={conceptId}", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<KnowledgeRelationshipPredicateDto>> GetKnowledgeRelationshipPredicatesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<KnowledgeRelationshipPredicateDto>>("api/knowledge/concepts/relationship-predicates", cancellationToken) ?? [];

    public Task UpdateKnowledgeRelationshipAsync(Guid id, UpdateConceptRelationshipCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/concepts/relationships/{id}", command, cancellationToken);

    public Task DeleteKnowledgeRelationshipAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/concepts/relationships/{id}", command, cancellationToken);

    public async Task<IReadOnlyCollection<ConceptHierarchyNode>> GetKnowledgeAncestorsAsync(Guid conceptId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<ConceptHierarchyNode>>($"api/knowledge/concepts/{conceptId}/ancestors", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<ConceptHierarchyNode>> GetKnowledgeDescendantsAsync(Guid conceptId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<ConceptHierarchyNode>>($"api/knowledge/concepts/{conceptId}/descendants", cancellationToken) ?? [];

    public Task<KnowledgeDescendantResult?> IsKnowledgeDescendantAsync(Guid conceptId, Guid ancestorId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<KnowledgeDescendantResult>($"api/knowledge/concepts/{conceptId}/is-descendant-of/{ancestorId}", cancellationToken);

    public Task<PagedResult<ExternalConceptMappingDto>?> SearchKnowledgeMappingsAsync(string? searchTerm = null, string? sourceSystemTypeCode = null, bool? isApproved = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ExternalConceptMappingDto>>($"api/knowledge/mappings?searchTerm={Encode(searchTerm)}&sourceSystemTypeCode={Encode(sourceSystemTypeCode)}&isApproved={isApproved}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateKnowledgeMappingAsync(CreateExternalMappingCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync("api/knowledge/mappings", command, cancellationToken);

    public Task UpdateKnowledgeMappingAsync(Guid id, UpdateExternalMappingCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/mappings/{id}", command, cancellationToken);

    public Task DeleteKnowledgeMappingAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/mappings/{id}", command, cancellationToken);

    public async Task<ExternalMappingResult?> ResolveKnowledgeMappingAsync(ExternalMappingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/knowledge/mappings/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalMappingResult>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<MappingReviewDto>?> SearchKnowledgeReviewsAsync(string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MappingReviewDto>>($"api/knowledge/reviews?statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task ReviewKnowledgeMappingAsync(Guid reviewId, ReviewExternalMappingCommand command, CancellationToken cancellationToken = default)
        => (await _httpClient.PostAsJsonAsync($"api/knowledge/reviews/{reviewId}/decision", command, cancellationToken)).EnsureSuccessStatusCode();

    public async Task<Guid> QueueKnowledgeImportAsync(QueueKnowledgeImportCommand command, CancellationToken cancellationToken = default)
        => await PostForKnowledgeIdAsync("api/knowledge/imports", command, cancellationToken);

    public Task UpdateKnowledgeImportAsync(Guid id, UpdateKnowledgeImportCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/imports/{id}", command, cancellationToken);

    public Task DeleteKnowledgeImportAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/imports/{id}", command, cancellationToken);

    public Task<PagedResult<KnowledgeImportDto>?> SearchKnowledgeImportsAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<KnowledgeImportDto>>($"api/knowledge/imports?searchTerm={Encode(searchTerm)}&statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task PublishKnowledgeAsync(Guid publicationId, PublishKnowledgeCommand command, CancellationToken cancellationToken = default)
        => (await _httpClient.PostAsJsonAsync($"api/knowledge/publications/{publicationId}/publish", command, cancellationToken)).EnsureSuccessStatusCode();

    public Task<PagedResult<KnowledgePublicationDto>?> SearchKnowledgePublicationsAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<KnowledgePublicationDto>>($"api/knowledge/publications?searchTerm={Encode(searchTerm)}&statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<Guid> CreateKnowledgePublicationAsync(CreateKnowledgePublicationCommand command, CancellationToken cancellationToken = default)
        => PostForKnowledgeIdAsync("api/knowledge/publications", command, cancellationToken);

    public Task UpdateKnowledgePublicationAsync(Guid id, UpdateKnowledgePublicationCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/publications/{id}", command, cancellationToken);

    public Task DeleteKnowledgePublicationAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/publications/{id}", command, cancellationToken);

    public Task<PagedResult<KnowledgeValidationRuleDto>?> SearchKnowledgeValidationRulesAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<KnowledgeValidationRuleDto>>($"api/knowledge/rules?searchTerm={Encode(searchTerm)}&statusCode={Encode(statusCode)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<Guid> CreateKnowledgeValidationRuleAsync(CreateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default)
        => PostForKnowledgeIdAsync("api/knowledge/rules", command, cancellationToken);

    public Task UpdateKnowledgeValidationRuleAsync(Guid id, UpdateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default)
        => PutKnowledgeAsync($"api/knowledge/rules/{id}", command, cancellationToken);

    public Task DeleteKnowledgeValidationRuleAsync(Guid id, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => DeleteKnowledgeAsync($"api/knowledge/rules/{id}", command, cancellationToken);

    public async Task<ConceptResolutionResult?> ResolveKnowledgeConceptAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/knowledge/semantic/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConceptResolutionResult>(cancellationToken: cancellationToken);
    }

    public async Task<SemanticValidationResult?> ValidateKnowledgeAsync(SemanticValidationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/knowledge/semantic/validate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SemanticValidationResult>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<KnowledgeLookupDto>> GetKnowledgeLookupsAsync(string lookupTypeCode, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyCollection<KnowledgeLookupDto>>($"api/knowledge/lookups/{Uri.EscapeDataString(lookupTypeCode)}", cancellationToken) ?? [];

    public Task<PagedResult<KnowledgeAuditDto>?> SearchKnowledgeAuditAsync(string? searchTerm = null, string? actionType = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<KnowledgeAuditDto>>($"api/knowledge/audit?searchTerm={Encode(searchTerm)}&actionType={Encode(actionType)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    private async Task<Guid> PostForKnowledgeIdAsync<T>(string uri, T request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeIdResult>(cancellationToken: cancellationToken))!.Id;
    }

    private async Task PutKnowledgeAsync<T>(string uri, T request, CancellationToken cancellationToken)
        => (await _httpClient.PutAsJsonAsync(uri, request, cancellationToken)).EnsureSuccessStatusCode();

    private async Task DeleteKnowledgeAsync<T>(string uri, T request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, uri) { Content = JsonContent.Create(request) };
        (await _httpClient.SendAsync(message, cancellationToken)).EnsureSuccessStatusCode();
    }

    private static string Encode(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    public sealed record KnowledgeDescendantResult(bool IsDescendant);
    private sealed record KnowledgeIdResult(Guid Id);
}
