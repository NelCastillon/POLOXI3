using Ams.Knowledge.Application.Common.Models;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Ams.Knowledge.Contracts.Validation;
using Ams.Knowledge.Domain.Concepts;
using Ams.Knowledge.Domain.Governance;
using Ams.Knowledge.Domain.Mappings;

namespace Ams.Knowledge.Application.Abstractions.Persistence;

public interface IKnowledgeQueryRepository
{
    Task<KnowledgeDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<ConceptSchemeDto>> SearchSchemesAsync(SearchConceptSchemesQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<KnowledgeConceptDto>> SearchConceptsAsync(SearchKnowledgeConceptsQuery query, CancellationToken cancellationToken = default);
    Task<KnowledgeConceptDto?> GetConceptAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptLabelDto>> GetLabelsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptRelationshipDto>> GetRelationshipsAsync(Guid tenantId, Guid? conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<KnowledgeRelationshipPredicateDto>> GetRelationshipPredicatesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<ExternalConceptMappingDto>> SearchMappingsAsync(SearchMappingsQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<MappingReviewDto>> SearchMappingReviewsAsync(SearchMappingReviewsQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<KnowledgeAuditDto>> SearchAuditAsync(SearchKnowledgeAuditQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<KnowledgeImportDto>> SearchImportsAsync(SearchKnowledgeImportsQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<KnowledgePublicationDto>> SearchPublicationsAsync(SearchKnowledgePublicationsQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<KnowledgeValidationRuleDto>> SearchValidationRulesAsync(Guid tenantId, string? searchTerm, string? statusCode, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<KnowledgeLookupDto>> GetLookupsAsync(GetKnowledgeLookupsQuery query, CancellationToken cancellationToken = default);
}

public interface IKnowledgeCommandRepository
{
    Task<KnowledgeConcept?> GetConceptAggregateAsync(Guid contextTenantId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<Guid> CreateSchemeAsync(ConceptScheme scheme, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateSchemeAsync(UpdateConceptSchemeCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> CreateConceptAsync(KnowledgeConcept concept, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateConceptAsync(KnowledgeConcept concept, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> AddLabelAsync(ConceptLabel label, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateLabelAsync(UpdateConceptLabelCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> AddRelationshipAsync(ConceptRelationship relationship, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateRelationshipAsync(UpdateConceptRelationshipCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> CreateMappingAsync(ExternalConceptMapping mapping, MappingReview review, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateMappingAsync(UpdateExternalMappingCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task ReviewMappingAsync(Guid tenantId, Guid reviewId, Guid mappingId, string decisionStatusCode, string reason, Guid reviewerUserId, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> QueueImportAsync(KnowledgeImportJob importJob, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateImportAsync(UpdateKnowledgeImportCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> CreateValidationRuleAsync(CreateKnowledgeValidationRuleCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdateValidationRuleAsync(UpdateKnowledgeValidationRuleCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task<Guid> CreatePublicationAsync(CreateKnowledgePublicationCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task UpdatePublicationAsync(UpdateKnowledgePublicationCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(string entityType, DeleteKnowledgeEntityCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task PublishAsync(Guid contextTenantId, Guid publicationId, string publishedStatusCode, string changeReason, Guid actorUserId, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default);
    Task EnsureSchemeAccessibleAsync(Guid contextTenantId, Guid schemeId, CancellationToken cancellationToken = default);
    Task EnsureConceptsAccessibleAsync(Guid contextTenantId, IReadOnlyCollection<Guid> conceptIds, CancellationToken cancellationToken = default);
    Task<RelationshipPredicateBehavior> GetPredicateBehaviorAsync(string predicateCode, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<(Guid ParentConceptId, Guid ChildConceptId)>> GetApprovedHierarchyEdgesAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

public sealed record RelationshipPredicateBehavior(bool IsHierarchical, bool SubjectIsChild);

public interface IConceptResolutionRepository
{
    Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedExternalCandidatesAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptCandidate>> FindPreferredLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptCandidate>> FindContextualCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptCandidate>> FindFuzzyCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, int maximumCandidates, CancellationToken cancellationToken = default);
}

public sealed record KnowledgeResolutionPolicy(decimal AutoResolveThreshold, decimal ReviewThreshold, int MaximumCandidates);
public sealed record DocumentFieldSchemeRoute(string? PathContains,string? PathSuffix,string SchemeCode);

public interface IKnowledgeDocumentRoutingProvider
{
    Task<IReadOnlyCollection<DocumentFieldSchemeRoute>> GetRoutesAsync(Guid tenantId,CancellationToken cancellationToken=default);
}

public interface IKnowledgeResolutionPolicyProvider
{
    Task<KnowledgeResolutionPolicy> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IKnowledgeHierarchyRepository
{
    Task<bool> IsDescendantOfAsync(Guid tenantId, Guid conceptId, Guid ancestorConceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptHierarchyNode>> GetAncestorsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptHierarchyNode>> GetDescendantsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
}

public interface IExternalMappingRepository
{
    Task<ExternalMappingResult?> ResolveApprovedAsync(ExternalMappingRequest request, CancellationToken cancellationToken = default);
}

public sealed record SemanticValidationRuleDefinition(Guid RuleId, string RuleCode, string RuleTypeCode, string? PropertyPath, string OperatorCode, string? ExpectedValue, int? MinimumCount, int? MaximumCount, string SeverityCode, string Message);

public interface IKnowledgeValidationRuleRepository
{
    Task<IReadOnlyCollection<SemanticValidationRuleDefinition>> GetEffectiveRulesAsync(Guid tenantId, Guid appliesToConceptId, DateTime effectiveUtc, CancellationToken cancellationToken = default);
}

public interface IKnowledgeValidationPolicyProvider
{
    Task<IReadOnlySet<string>> GetBlockingSeverityCodesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ISemanticRuleEvaluator
{
    SemanticValidationIssue? Evaluate(SemanticValidationRuleDefinition rule, SemanticValidationRequest request);
}
