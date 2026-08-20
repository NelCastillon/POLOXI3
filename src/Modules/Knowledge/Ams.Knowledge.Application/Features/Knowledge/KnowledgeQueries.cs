namespace Ams.Knowledge.Application.Features.Knowledge;

public sealed record SearchConceptSchemesQuery(Guid TenantId, string? SearchTerm, string? StatusCode, int PageNumber = 1, int PageSize = 50);
public sealed record SearchKnowledgeConceptsQuery(Guid TenantId, Guid? ConceptSchemeId, string? SearchTerm, string? ConceptTypeCode, string? StatusCode, int PageNumber = 1, int PageSize = 50);
public sealed record SearchMappingsQuery(Guid TenantId, string? SearchTerm, string? SourceSystemTypeCode, bool? IsApproved, int PageNumber = 1, int PageSize = 50);
public sealed record SearchMappingReviewsQuery(Guid TenantId, string? StatusCode, int PageNumber = 1, int PageSize = 50);
public sealed record GetKnowledgeLookupsQuery(string LookupTypeCode, Guid? TenantId);
public sealed record GetConceptHierarchyQuery(Guid TenantId, Guid ConceptId);
public sealed record SearchKnowledgeAuditQuery(Guid TenantId, string? SearchTerm, string? ActionType, int PageNumber = 1, int PageSize = 50);
public sealed record SearchKnowledgeImportsQuery(Guid TenantId, string? SearchTerm, string? StatusCode, int PageNumber = 1, int PageSize = 50);
public sealed record SearchKnowledgePublicationsQuery(Guid TenantId, string? SearchTerm, string? StatusCode, int PageNumber = 1, int PageSize = 50);
public sealed record SearchWorkflowGuideStepsQuery(Guid TenantId, string? SearchTerm, string? ModuleCode, string? StageName, bool IncludeOptional = true, int PageNumber = 1, int PageSize = 100);
