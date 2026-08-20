using Ams.Knowledge.Application.Common.Models;
using Ams.Knowledge.Application.Features.Knowledge;
using Dapper;

namespace Ams.Knowledge.Infrastructure.Persistence.Repositories;

public sealed partial class KnowledgeRepository
{
    public async Task<KnowledgeDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    (SELECT COUNT(*) FROM knowledge.ConceptScheme WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND StatusCode <> N'RETIRED') AS ActiveSchemes,
    (SELECT COUNT(*) FROM knowledge.KnowledgeConcept WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND StatusCode = N'PUBLISHED') AS PublishedConcepts,
    (SELECT COUNT(*) FROM knowledge.ConceptLabel WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND IsSearchable = 1 AND IsDeprecated = 0) AS SearchableLabels,
    (SELECT COUNT(*) FROM knowledge.ExternalConceptMapping WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsApproved = 1) AS ApprovedMappings,
    (SELECT COUNT(*) FROM knowledge.MappingReview WHERE IsDeleted = 0 AND TenantId = @TenantId AND StatusCode = N'PENDING') AS PendingMappingReviews,
    (SELECT COUNT(*) FROM knowledge.ChangeRequest WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND StatusCode IN (N'DRAFT', N'SUBMITTED', N'UNDER_REVIEW')) AS DraftChangeRequests,
    (SELECT COUNT(*) FROM knowledge.ImportJob WHERE IsDeleted = 0 AND TenantId = @TenantId AND StatusCode = N'FAILED') AS FailedImports;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<KnowledgeDashboardDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ConceptSchemeDto>> SearchSchemesAsync(SearchConceptSchemesQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ConceptSchemeId, SchemeCode, Name, Description, AuthorityCode, VersionLabel, StatusCode, TenantId, IsSystemDefined, RowVersion
INTO #Filtered
FROM knowledge.ConceptScheme
WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR SchemeCode LIKE '%' + @SearchTerm + '%' OR Name LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY Name OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<ConceptSchemeDto>(sql, new { query.TenantId, query.SearchTerm, query.StatusCode, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<KnowledgeConceptDto>> SearchConceptsAsync(SearchKnowledgeConceptsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, Definition, ParentConceptId,
       IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc, VersionNumber, SupersedesConceptId,
       TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId, TechnicalStewardUserId, DefinitionSource, LicensingNotes, RowVersion
INTO #Filtered
FROM knowledge.KnowledgeConcept
WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId)
  AND (@ConceptSchemeId IS NULL OR ConceptSchemeId = @ConceptSchemeId)
  AND (@ConceptTypeCode IS NULL OR @ConceptTypeCode = '' OR ConceptTypeCode = @ConceptTypeCode)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ConceptCode LIKE '%' + @SearchTerm + '%' OR PreferredLabel LIKE '%' + @SearchTerm + '%' OR Definition LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY PreferredLabel, VersionNumber DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<KnowledgeConceptDto>(sql, new { query.TenantId, query.ConceptSchemeId, query.SearchTerm, query.ConceptTypeCode, query.StatusCode, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<WorkflowGuideStepDto>> SearchWorkflowGuideStepsAsync(SearchWorkflowGuideStepsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
WITH RankedSteps AS
(
    SELECT WorkflowGuideStepId, WorkflowCode, StepCode, SequenceNumber, ModuleCode, ModuleSequenceNumber, ModuleDisplayName, StageName, StepTitle, StepDescription,
           PageName, PageRoute, NavigationRoute, ActionLabel, ActionTypeCode, FromStatusCode, ToStatusCode, ExpectedResult, NextUserMove,
           ValidationRequirements, AlternatePath, SearchKeywords, IsOptional, TenantId,
           ROW_NUMBER() OVER (PARTITION BY WorkflowCode, StepCode ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END) AS ScopeRank
    FROM knowledge.WorkflowGuideStep
    WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
)
    SELECT WorkflowGuideStepId, WorkflowCode, StepCode, SequenceNumber, ModuleCode, ModuleSequenceNumber, ModuleDisplayName, StageName, StepTitle, StepDescription,
           PageName, PageRoute, NavigationRoute, ActionLabel, ActionTypeCode, FromStatusCode, ToStatusCode, ExpectedResult, NextUserMove,
           ValidationRequirements, AlternatePath, SearchKeywords, IsOptional, TenantId
    INTO #Filtered
    FROM RankedSteps
    WHERE ScopeRank = 1
      AND (@ModuleCode IS NULL OR @ModuleCode = '' OR ModuleCode = @ModuleCode)
      AND (@StageName IS NULL OR @StageName = '' OR StageName = @StageName)
      AND (@IncludeOptional = 1 OR IsOptional = 0)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR StepTitle LIKE '%' + @SearchTerm + '%' OR StepDescription LIKE '%' + @SearchTerm + '%'
           OR PageName LIKE '%' + @SearchTerm + '%' OR PageRoute LIKE '%' + @SearchTerm + '%'
           OR ActionLabel LIKE '%' + @SearchTerm + '%' OR StageName LIKE '%' + @SearchTerm + '%'
           OR FromStatusCode LIKE '%' + @SearchTerm + '%' OR ToStatusCode LIKE '%' + @SearchTerm + '%'
           OR ExpectedResult LIKE '%' + @SearchTerm + '%' OR NextUserMove LIKE '%' + @SearchTerm + '%'
           OR ValidationRequirements LIKE '%' + @SearchTerm + '%' OR AlternatePath LIKE '%' + @SearchTerm + '%'
           OR SearchKeywords LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY ModuleSequenceNumber, SequenceNumber, StepTitle OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<WorkflowGuideStepDto>(sql, new { query.TenantId, query.SearchTerm, query.ModuleCode, query.StageName, query.IncludeOptional, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<KnowledgeConceptDto?> GetConceptAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, Definition, ParentConceptId,
       IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc, VersionNumber, SupersedesConceptId,
       TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId, TechnicalStewardUserId, DefinitionSource, LicensingNotes, RowVersion
FROM knowledge.KnowledgeConcept
WHERE KnowledgeConceptId = @ConceptId AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId);
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<KnowledgeConceptDto>(new CommandDefinition(sql, new { TenantId = tenantId, ConceptId = conceptId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<ConceptLabelDto>> GetLabelsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated, TenantId, RowVersion
FROM knowledge.ConceptLabel
WHERE KnowledgeConceptId = @ConceptId AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId)
ORDER BY CASE WHEN LabelTypeCode = N'PREFERRED' THEN 0 ELSE 1 END, Label;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ConceptLabelDto>(new CommandDefinition(sql, new { TenantId = tenantId, ConceptId = conceptId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<ConceptRelationshipDto>> GetRelationshipsAsync(Guid tenantId, Guid? conceptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ConceptRelationshipId, SubjectConceptId, PredicateCode, ObjectConceptId, RelationshipStrength, Source,
       EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId, RowVersion
FROM knowledge.ConceptRelationship
WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId)
  AND (@ConceptId IS NULL OR SubjectConceptId = @ConceptId OR ObjectConceptId = @ConceptId)
ORDER BY PredicateCode, EffectiveFromUtc DESC;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ConceptRelationshipDto>(new CommandDefinition(sql, new { TenantId = tenantId, ConceptId = conceptId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<KnowledgeRelationshipPredicateDto>> GetRelationshipPredicatesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PredicateCode, DisplayName, Description, IsHierarchical, SubjectIsChild, InversePredicateCode FROM knowledge.RelationshipPredicate WHERE IsActive = 1 ORDER BY DisplayName;";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<KnowledgeRelationshipPredicateDto>(new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PagedResult<ExternalConceptMappingDto>> SearchMappingsAsync(SearchMappingsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ExternalConceptMappingId, KnowledgeConceptId, SourceSystemTypeCode, SourceSystemId, ExternalCode, ExternalValue,
       MappingDirectionCode, MatchTypeCode, ConfidenceScore, StateCode, LineOfBusinessConceptId, CarrierProductId,
       IsApproved, ApprovedByUserId, ApprovedDateUtc AS ApprovedUtc, TenantId, RowVersion
INTO #Filtered
FROM knowledge.ExternalConceptMapping
WHERE IsDeleted = 0 AND TenantId = @TenantId
  AND (@SourceSystemTypeCode IS NULL OR @SourceSystemTypeCode = '' OR SourceSystemTypeCode = @SourceSystemTypeCode)
  AND (@IsApproved IS NULL OR IsApproved = @IsApproved)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ExternalCode LIKE '%' + @SearchTerm + '%' OR ExternalValue LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY ExternalValue OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<ExternalConceptMappingDto>(sql, new { query.TenantId, query.SearchTerm, query.SourceSystemTypeCode, query.IsApproved, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<MappingReviewDto>> SearchMappingReviewsAsync(SearchMappingReviewsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT MappingReviewId, ExternalConceptMappingId, StatusCode, RecommendationJson, ReviewedByUserId,
       ReviewedDateUtc AS ReviewedUtc, ReviewReason, TenantId, RowVersion
INTO #Filtered
FROM knowledge.MappingReview
WHERE IsDeleted = 0 AND TenantId = @TenantId AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode);
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY MappingReviewId OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<MappingReviewDto>(sql, new { query.TenantId, query.StatusCode, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<KnowledgeAuditDto>> SearchAuditAsync(SearchKnowledgeAuditQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT AuditEventId, TenantId, ActorUserId, ActionType, EntityName, EntityId, OldValue, NewValue,
       CorrelationId, ChangeReason, VersionNumber, CreatedUtc
INTO #Filtered
FROM Audit.AuditEvent
WHERE TenantId = @TenantId AND ModuleName = N'Knowledge'
  AND (@ActionType IS NULL OR @ActionType = '' OR ActionType = @ActionType)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR EntityName LIKE '%' + @SearchTerm + '%' OR CorrelationId LIKE '%' + @SearchTerm + '%' OR ChangeReason LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY CreatedUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<KnowledgeAuditDto>(sql, new { query.TenantId, query.SearchTerm, query.ActionType, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<KnowledgeImportDto>> SearchImportsAsync(SearchKnowledgeImportsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
    SELECT ImportJobId, ImportTypeCode, SourceFileName, StorageReference, StatusCode, CorrelationId, RecordsReceived, RecordsProcessed,
           RecordsFailed, ErrorMessage, RetryCount, TenantId, CreatedDateUtc, RowVersion
INTO #Filtered
FROM knowledge.ImportJob
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR SourceFileName LIKE '%' + @SearchTerm + '%' OR CorrelationId LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<KnowledgeImportDto>(sql, new { query.TenantId, query.SearchTerm, query.StatusCode, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<KnowledgePublicationDto>> SearchPublicationsAsync(SearchKnowledgePublicationsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT publication.PublicationId, publication.PublicationCode, publication.Name, publication.VersionLabel,
       publication.StatusCode, publication.TenantId, publication.IsSystemDefined, publication.PublishedByUserId,
       publication.PublishedDateUtc, COUNT(item.PublicationItemId) AS ItemCount, publication.RowVersion
INTO #Filtered
FROM knowledge.Publication publication
LEFT JOIN knowledge.PublicationItem item ON item.PublicationId = publication.PublicationId
WHERE publication.IsDeleted = 0 AND (publication.TenantId IS NULL OR publication.TenantId = @TenantId)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR publication.StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR publication.PublicationCode LIKE '%' + @SearchTerm + '%' OR publication.Name LIKE '%' + @SearchTerm + '%')
GROUP BY publication.PublicationId, publication.PublicationCode, publication.Name, publication.VersionLabel,
         publication.StatusCode, publication.TenantId, publication.IsSystemDefined, publication.PublishedByUserId,
         publication.PublishedDateUtc, publication.RowVersion;
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY Name, VersionLabel DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<KnowledgePublicationDto>(sql, new { query.TenantId, query.SearchTerm, query.StatusCode, Offset = Offset(query.PageNumber, query.PageSize), PageSize = PageSize(query.PageSize) }, query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<PagedResult<KnowledgeValidationRuleDto>> SearchValidationRulesAsync(Guid tenantId, string? searchTerm, string? statusCode, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ConceptValidationRuleId, AppliesToConceptId, RuleCode, RuleTypeCode, PropertyPath, OperatorCode, ExpectedValue,
       MinimumCount, MaximumCount, SeverityCode, Message, EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId,
       IsSystemDefined, RowVersion
INTO #Filtered
FROM knowledge.ConceptValidationRule
WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR RuleCode LIKE '%' + @SearchTerm + '%' OR Message LIKE '%' + @SearchTerm + '%');
SELECT COUNT(*) FROM #Filtered;
SELECT * FROM #Filtered ORDER BY RuleCode OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";
        return await QueryPagedAsync<KnowledgeValidationRuleDto>(sql, new { TenantId = tenantId, SearchTerm = searchTerm, StatusCode = statusCode, Offset = Offset(pageNumber, pageSize), PageSize = PageSize(pageSize) }, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyCollection<KnowledgeLookupDto>> GetLookupsAsync(GetKnowledgeLookupsQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
;WITH Ranked AS
(
    SELECT LookupTypeCode, ValueCode, DisplayName, Description, SortOrder, IsActive,
           ROW_NUMBER() OVER (PARTITION BY LookupTypeCode, ValueCode ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END) AS Priority
    FROM knowledge.LookupValue
    WHERE LookupTypeCode = @LookupTypeCode AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
)
SELECT LookupTypeCode, ValueCode, DisplayName, Description, SortOrder, IsActive
FROM Ranked WHERE Priority = 1 ORDER BY SortOrder, DisplayName;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<KnowledgeLookupDto>(new CommandDefinition(sql, new { query.LookupTypeCode, query.TenantId }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task<PagedResult<T>> QueryPagedAsync<T>(string sql, object parameters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<T>()).AsList();
        return new PagedResult<T>(items, total, Math.Max(1, pageNumber), PageSize(pageSize));
    }

    private static int Offset(int pageNumber, int pageSize) => (Math.Max(1, pageNumber) - 1) * PageSize(pageSize);
    private static int PageSize(int pageSize) => Math.Clamp(pageSize, 1, 250);
}
