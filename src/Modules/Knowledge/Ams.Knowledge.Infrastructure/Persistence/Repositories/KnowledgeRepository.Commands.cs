using System.Data;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Domain.Concepts;
using Ams.Knowledge.Domain.Governance;
using Ams.Knowledge.Domain.Mappings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ams.Knowledge.Infrastructure.Persistence.Repositories;

public sealed partial class KnowledgeRepository
{
    public async Task EnsureSchemeAccessibleAsync(Guid contextTenantId, Guid schemeId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(1) FROM knowledge.ConceptScheme WHERE ConceptSchemeId = @SchemeId AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @ContextTenantId);";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { ContextTenantId = contextTenantId, SchemeId = schemeId }, cancellationToken: cancellationToken)) == 0)
            throw new KeyNotFoundException("The Knowledge concept scheme was not found in the tenant scope.");
    }

    public async Task EnsureConceptsAccessibleAsync(Guid contextTenantId, IReadOnlyCollection<Guid> conceptIds, CancellationToken cancellationToken = default)
    {
        var distinctIds = conceptIds.Distinct().ToArray();
        const string sql = "SELECT COUNT(1) FROM knowledge.KnowledgeConcept WHERE KnowledgeConceptId IN @ConceptIds AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @ContextTenantId);";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { ContextTenantId = contextTenantId, ConceptIds = distinctIds }, cancellationToken: cancellationToken));
        if (count != distinctIds.Length)
            throw new KeyNotFoundException("One or more Knowledge concepts were not found in the tenant scope.");
    }

    public async Task<KnowledgeConcept?> GetConceptAggregateAsync(Guid contextTenantId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, Definition, ParentConceptId,
       IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc, VersionNumber, SupersedesConceptId,
       TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId, TechnicalStewardUserId, DefinitionSource,
       LicensingNotes, CreatedByUserId, CreatedDateUtc
FROM knowledge.KnowledgeConcept
WHERE KnowledgeConceptId = @ConceptId AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @ContextTenantId);

SELECT ConceptLabelId, KnowledgeConceptId, Label, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated,
       TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc
FROM knowledge.ConceptLabel
WHERE KnowledgeConceptId = @ConceptId AND IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @ContextTenantId);
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { ContextTenantId = contextTenantId, ConceptId = conceptId }, cancellationToken: cancellationToken));
        var row = await multi.ReadSingleOrDefaultAsync<ConceptPersistenceRow>();
        if (row is null)
            return null;

        var concept = new KnowledgeConcept(row.KnowledgeConceptId, row.ConceptSchemeId, row.ConceptCode, row.ConceptTypeCode, row.PreferredLabel, row.Definition, row.ParentConceptId, row.IsAbstract, row.IsSelectable, row.StatusCode, row.EffectiveFromUtc, row.EffectiveToUtc, row.VersionNumber, row.SupersedesConceptId, row.TenantId, row.IsSystemDefined, row.OwnerUserId, row.BusinessStewardUserId, row.TechnicalStewardUserId, row.DefinitionSource, row.LicensingNotes, row.CreatedByUserId, row.CreatedDateUtc);
        var labels = await multi.ReadAsync<ConceptLabelPersistenceRow>();
        foreach (var label in labels)
        {
            concept.AddLabel(new ConceptLabel(label.ConceptLabelId, label.KnowledgeConceptId, label.Label, label.LabelTypeCode, label.LanguageCode, label.Source, label.IsSearchable, label.IsDeprecated, label.TenantId, label.IsSystemDefined, label.CreatedByUserId, label.CreatedDateUtc));
        }
        return concept;
    }

    public async Task<Guid> CreateSchemeAsync(ConceptScheme scheme, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.ConceptScheme
(ConceptSchemeId, SchemeCode, Name, Description, AuthorityCode, VersionLabel, StatusCode, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @SchemeCode, @Name, @Description, @AuthorityCode, @VersionLabel, @StatusCode, @TenantId, @IsSystemDefined, @CreatedByUserId, @CreatedUtc, 0);
""";
        await ExecuteWriteAsync(sql, scheme, audit, cancellationToken);
        return scheme.Id;
    }

    public Task UpdateSchemeAsync(UpdateConceptSchemeCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ConceptScheme
SET Name = @Name, Description = @Description, AuthorityCode = @AuthorityCode, VersionLabel = @VersionLabel,
    StatusCode = @StatusCode, ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ConceptSchemeId = @ConceptSchemeId AND TenantId = @ContextTenantId AND IsSystemDefined = 0
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge concept scheme", cancellationToken);

    public async Task<Guid> CreateConceptAsync(KnowledgeConcept concept, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.KnowledgeConcept
(KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, NormalizedPreferredLabel, Definition, ParentConceptId, IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc, VersionNumber, SupersedesConceptId, TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId, TechnicalStewardUserId, DefinitionSource, LicensingNotes, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @ConceptSchemeId, @ConceptCode, @ConceptTypeCode, @PreferredLabel, UPPER(LTRIM(RTRIM(@PreferredLabel))), @Definition, @ParentConceptId, @IsAbstract, @IsSelectable, @StatusCode, @EffectiveFromUtc, @EffectiveToUtc, @VersionNumber, @SupersedesConceptId, @TenantId, @IsSystemDefined, @OwnerUserId, @BusinessStewardUserId, @TechnicalStewardUserId, @DefinitionSource, @LicensingNotes, @CreatedByUserId, @CreatedUtc, 0);

INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(NEWID(), @Id, @PreferredLabel, UPPER(LTRIM(RTRIM(@PreferredLabel))), N'PREFERRED', N'en-US', @DefinitionSource, 1, 0, @TenantId, @IsSystemDefined, @CreatedByUserId, @CreatedUtc, 0);
""";
        await ExecuteWriteAsync(sql, concept, audit, cancellationToken);
        return concept.Id;
    }

    public async Task UpdateConceptAsync(KnowledgeConcept concept, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE knowledge.KnowledgeConcept
SET ConceptTypeCode = @ConceptTypeCode, PreferredLabel = @PreferredLabel,
    NormalizedPreferredLabel = UPPER(LTRIM(RTRIM(@PreferredLabel))), Definition = @Definition,
    ParentConceptId = @ParentConceptId, IsAbstract = @IsAbstract, IsSelectable = @IsSelectable,
    EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc,
    OwnerUserId = @OwnerUserId, BusinessStewardUserId = @BusinessStewardUserId,
    TechnicalStewardUserId = @TechnicalStewardUserId, DefinitionSource = @DefinitionSource,
    LicensingNotes = @LicensingNotes, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = @ModifiedUtc
WHERE KnowledgeConceptId = @Id AND TenantId = @ContextTenantId AND IsDeleted = 0 AND RowVersion = @ExpectedRowVersion;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                concept.Id, concept.ConceptTypeCode, concept.PreferredLabel, concept.Definition, concept.ParentConceptId,
                concept.IsAbstract, concept.IsSelectable, concept.EffectiveFromUtc, concept.EffectiveToUtc,
                concept.OwnerUserId, concept.BusinessStewardUserId, concept.TechnicalStewardUserId,
                concept.DefinitionSource, concept.LicensingNotes, concept.ModifiedByUserId, concept.ModifiedUtc,
                ContextTenantId = audit.TenantId, ExpectedRowVersion = expectedRowVersion
            }, transaction, cancellationToken: cancellationToken));
            EnsureSingleRow(affected, "Knowledge concept");
            await WriteAuditAndOutboxAsync(connection, transaction, audit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidateTenant(audit.TenantId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Guid> AddLabelAsync(ConceptLabel label, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @KnowledgeConceptId, @Label, @NormalizedLabel, @LabelTypeCode, @LanguageCode, @Source, @IsSearchable, @IsDeprecated, @TenantId, @IsSystemDefined, @CreatedByUserId, @CreatedUtc, 0);
""";
        await ExecuteWriteAsync(sql, label, audit, cancellationToken);
        return label.Id;
    }

    public Task UpdateLabelAsync(UpdateConceptLabelCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ConceptLabel
SET Label = @Label, NormalizedLabel = UPPER(LTRIM(RTRIM(@Label))), LabelTypeCode = @LabelTypeCode,
    LanguageCode = @LanguageCode, Source = @Source, IsSearchable = @IsSearchable, IsDeprecated = @IsDeprecated,
    ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ConceptLabelId = @ConceptLabelId AND TenantId = @ContextTenantId AND IsSystemDefined = 0
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge concept label", cancellationToken);

    public async Task<Guid> AddRelationshipAsync(ConceptRelationship relationship, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.ConceptRelationship
(ConceptRelationshipId, SubjectConceptId, PredicateCode, ObjectConceptId, RelationshipStrength, Source, EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @SubjectConceptId, @PredicateCode, @ObjectConceptId, @RelationshipStrength, @Source, @EffectiveFromUtc, @EffectiveToUtc, @StatusCode, @TenantId, @IsSystemDefined, @CreatedByUserId, @CreatedUtc, 0);

IF EXISTS (SELECT 1 FROM knowledge.RelationshipPredicate WHERE PredicateCode = @PredicateCode AND IsHierarchical = 1 AND IsActive = 1)
BEGIN
    DECLARE @HierarchyLockResult INT;
    EXEC @HierarchyLockResult = sys.sp_getapplock @Resource = N'Ams.Knowledge.HierarchyRebuild', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 30000;
    IF @HierarchyLockResult < 0 THROW 51010, 'Could not acquire the Knowledge hierarchy rebuild lock.', 1;

    DELETE FROM knowledge.ConceptHierarchyClosure;
    ;WITH Edges AS
    (
        SELECT ParentConceptId AS ParentId, KnowledgeConceptId AS ChildId
        FROM knowledge.KnowledgeConcept
        WHERE ParentConceptId IS NOT NULL AND IsDeleted = 0 AND StatusCode IN (N'APPROVED', N'PUBLISHED')
        UNION
        SELECT CASE WHEN predicate.SubjectIsChild = 1 THEN relation.ObjectConceptId ELSE relation.SubjectConceptId END,
               CASE WHEN predicate.SubjectIsChild = 1 THEN relation.SubjectConceptId ELSE relation.ObjectConceptId END
        FROM knowledge.ConceptRelationship relation
        INNER JOIN knowledge.RelationshipPredicate predicate ON predicate.PredicateCode = relation.PredicateCode
        WHERE predicate.IsHierarchical = 1 AND predicate.IsActive = 1 AND relation.IsDeleted = 0 AND relation.StatusCode IN (N'APPROVED', N'PUBLISHED')
    ), Hierarchy AS
    (
        SELECT KnowledgeConceptId AS AncestorId, KnowledgeConceptId AS DescendantId, 0 AS Depth
        FROM knowledge.KnowledgeConcept WHERE IsDeleted = 0 AND StatusCode IN (N'APPROVED', N'PUBLISHED')
        UNION ALL
        SELECT hierarchy.AncestorId, edge.ChildId, hierarchy.Depth + 1
        FROM Hierarchy hierarchy INNER JOIN Edges edge ON edge.ParentId = hierarchy.DescendantId
    )
    INSERT INTO knowledge.ConceptHierarchyClosure(AncestorConceptId, DescendantConceptId, Depth, RefreshedDateUtc)
    SELECT AncestorId, DescendantId, MIN(Depth), SYSUTCDATETIME() FROM Hierarchy GROUP BY AncestorId, DescendantId
    OPTION (MAXRECURSION 32767);
END;
""";
        await ExecuteWriteAsync(sql, relationship, audit, cancellationToken);
        return relationship.Id;
    }

    public Task UpdateRelationshipAsync(UpdateConceptRelationshipCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ConceptRelationship
SET PredicateCode = @PredicateCode, RelationshipStrength = @RelationshipStrength, Source = @Source,
    EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc, StatusCode = @StatusCode,
    ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ConceptRelationshipId = @ConceptRelationshipId AND TenantId = @ContextTenantId AND IsSystemDefined = 0
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge concept relationship", cancellationToken);

    public async Task<Guid> CreateMappingAsync(ExternalConceptMapping mapping, MappingReview review, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.ExternalConceptMapping
(ExternalConceptMappingId, KnowledgeConceptId, SourceSystemTypeCode, SourceSystemId, ExternalCode, ExternalValue, NormalizedExternalValue, ExternalPath, MappingDirectionCode, MatchTypeCode, ConfidenceScore, StateCode, LineOfBusinessConceptId, CarrierProductId, EffectiveFromUtc, EffectiveToUtc, IsApproved, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@MappingId, @KnowledgeConceptId, @SourceSystemTypeCode, @SourceSystemId, @ExternalCode, @ExternalValue, @NormalizedExternalValue, @ExternalPath, @MappingDirectionCode, @MatchTypeCode, @ConfidenceScore, @StateCode, @LineOfBusinessConceptId, @CarrierProductId, @EffectiveFromUtc, @EffectiveToUtc, 0, @TenantId, @IsSystemDefined, @CreatedByUserId, @CreatedUtc, 0);

INSERT INTO knowledge.MappingReview
(MappingReviewId, ExternalConceptMappingId, TenantId, StatusCode, RecommendationJson, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@ReviewId, @MappingId, @TenantId, @ReviewStatusCode, @RecommendationJson, @CreatedByUserId, @CreatedUtc, 0);
""";
        var parameters = new
        {
            MappingId = mapping.Id, mapping.KnowledgeConceptId, mapping.SourceSystemTypeCode, mapping.SourceSystemId,
            mapping.ExternalCode, mapping.ExternalValue, mapping.NormalizedExternalValue, mapping.ExternalPath,
            mapping.MappingDirectionCode, mapping.MatchTypeCode, mapping.ConfidenceScore, mapping.StateCode,
            mapping.LineOfBusinessConceptId, mapping.CarrierProductId, mapping.EffectiveFromUtc, mapping.EffectiveToUtc,
            mapping.TenantId, mapping.IsSystemDefined, mapping.CreatedByUserId, mapping.CreatedUtc,
            ReviewId = review.Id, ReviewStatusCode = review.StatusCode, review.RecommendationJson
        };
        await ExecuteWriteAsync(sql, parameters, audit, cancellationToken);
        return mapping.Id;
    }

    public Task UpdateMappingAsync(UpdateExternalMappingCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ExternalConceptMapping
SET KnowledgeConceptId = @KnowledgeConceptId, SourceSystemTypeCode = @SourceSystemTypeCode, SourceSystemId = @SourceSystemId,
    ExternalCode = @ExternalCode, ExternalValue = @ExternalValue, NormalizedExternalValue = UPPER(LTRIM(RTRIM(@ExternalValue))),
    ExternalPath = @ExternalPath, MappingDirectionCode = @MappingDirectionCode, MatchTypeCode = @MatchTypeCode,
    ConfidenceScore = @ConfidenceScore, StateCode = @StateCode, LineOfBusinessConceptId = @LineOfBusinessConceptId,
    CarrierProductId = @CarrierProductId, EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc,
    IsApproved = 0, ApprovedByUserId = NULL, ApprovedDateUtc = NULL,
    ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ExternalConceptMappingId = @ExternalConceptMappingId AND TenantId = @TenantId
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge external mapping", cancellationToken);

    public async Task ReviewMappingAsync(Guid tenantId, Guid reviewId, Guid mappingId, string decisionStatusCode, string reason, Guid reviewerUserId, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE knowledge.MappingReview
SET StatusCode = @DecisionStatusCode, ReviewReason = @Reason, ReviewedByUserId = @ReviewerUserId,
    ReviewedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ReviewerUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE MappingReviewId = @ReviewId AND ExternalConceptMappingId = @MappingId AND TenantId = @TenantId AND IsDeleted = 0 AND RowVersion = @ExpectedRowVersion;

IF @@ROWCOUNT = 0 THROW 51001, 'The mapping review was changed or removed by another user.', 1;

UPDATE knowledge.ExternalConceptMapping
SET IsApproved = CASE WHEN @DecisionStatusCode = N'APPROVED' THEN 1 ELSE 0 END,
    ApprovedByUserId = CASE WHEN @DecisionStatusCode = N'APPROVED' THEN @ReviewerUserId ELSE NULL END,
    ApprovedDateUtc = CASE WHEN @DecisionStatusCode = N'APPROVED' THEN SYSUTCDATETIME() ELSE NULL END,
    ModifiedByUserId = @ReviewerUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ExternalConceptMappingId = @MappingId AND TenantId = @TenantId AND IsDeleted = 0;
""";
        await ExecuteWriteAsync(sql, new { TenantId = tenantId, ReviewId = reviewId, MappingId = mappingId, DecisionStatusCode = decisionStatusCode, Reason = reason, ReviewerUserId = reviewerUserId, ExpectedRowVersion = expectedRowVersion }, audit, cancellationToken);
    }

    public async Task<Guid> QueueImportAsync(KnowledgeImportJob importJob, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT INTO knowledge.ImportJob
(ImportJobId, TenantId, ImportTypeCode, SourceFileName, StorageReference, StatusCode, CorrelationId, RecordsReceived, RecordsProcessed, RecordsFailed, RetryCount, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @TenantId, @ImportTypeCode, @SourceFileName, @StorageReference, @StatusCode, @CorrelationId, 0, 0, 0, 0, @CreatedByUserId, @CreatedUtc, 0);
""";
        await ExecuteWriteAsync(sql, importJob, audit, cancellationToken);
        return importJob.Id;
    }

    public Task UpdateImportAsync(UpdateKnowledgeImportCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ImportJob
SET ImportTypeCode = @ImportTypeCode, SourceFileName = @SourceFileName, StorageReference = @StorageReference,
    CorrelationId = @CorrelationId, ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ImportJobId = @ImportJobId AND TenantId = @TenantId AND StatusCode IN (N'QUEUED', N'FAILED')
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge import", cancellationToken);

    public async Task<Guid> CreateValidationRuleAsync(CreateKnowledgeValidationRuleCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var securedAudit = audit with { EntityId = id };
        const string sql = """
INSERT INTO knowledge.ConceptValidationRule
(ConceptValidationRuleId, AppliesToConceptId, RuleCode, RuleTypeCode, PropertyPath, OperatorCode, ExpectedValue,
 MinimumCount, MaximumCount, SeverityCode, Message, EffectiveFromUtc, EffectiveToUtc, StatusCode, TenantId,
 IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
(@Id, @AppliesToConceptId, @RuleCode, @RuleTypeCode, @PropertyPath, @OperatorCode, @ExpectedValue,
 @MinimumCount, @MaximumCount, @SeverityCode, @Message, @EffectiveFromUtc, @EffectiveToUtc, @StatusCode,
 @ContextTenantId, 0, @ActorUserId, SYSUTCDATETIME(), 0);
""";
        await ExecuteWriteAsync(sql, new { Id = id, command.AppliesToConceptId, command.RuleCode, command.RuleTypeCode, command.PropertyPath, command.OperatorCode, command.ExpectedValue, command.MinimumCount, command.MaximumCount, command.SeverityCode, command.Message, command.EffectiveFromUtc, command.EffectiveToUtc, command.StatusCode, command.ContextTenantId, command.ActorUserId }, securedAudit, cancellationToken);
        return id;
    }

    public Task UpdateValidationRuleAsync(UpdateKnowledgeValidationRuleCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.ConceptValidationRule
SET AppliesToConceptId = @AppliesToConceptId, RuleTypeCode = @RuleTypeCode, PropertyPath = @PropertyPath,
    OperatorCode = @OperatorCode, ExpectedValue = @ExpectedValue, MinimumCount = @MinimumCount, MaximumCount = @MaximumCount,
    SeverityCode = @SeverityCode, Message = @Message, EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc,
    StatusCode = @StatusCode, ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ConceptValidationRuleId = @ConceptValidationRuleId AND TenantId = @ContextTenantId AND IsSystemDefined = 0
  AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge validation rule", cancellationToken);

    public async Task<Guid> CreatePublicationAsync(CreateKnowledgePublicationCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var securedAudit = audit with { EntityId = id };
        const string sql = """
INSERT INTO knowledge.Publication
(PublicationId, PublicationCode, Name, VersionLabel, StatusCode, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@Id, @PublicationCode, @Name, @VersionLabel, @StatusCode, @ContextTenantId, 0, @ActorUserId, SYSUTCDATETIME(), 0);
""";
        await ExecuteWriteAsync(sql, new { Id = id, command.PublicationCode, command.Name, command.VersionLabel, command.StatusCode, command.ContextTenantId, command.ActorUserId }, securedAudit, cancellationToken);
        return id;
    }

    public Task UpdatePublicationAsync(UpdateKnowledgePublicationCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
        => ExecuteAuditedMutationAsync("""
UPDATE knowledge.Publication
SET Name = @Name, VersionLabel = @VersionLabel, StatusCode = @StatusCode,
    ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE PublicationId = @PublicationId AND TenantId = @ContextTenantId AND IsSystemDefined = 0
  AND PublishedDateUtc IS NULL AND IsDeleted = 0 AND RowVersion = @RowVersion;
""", command, audit, "Knowledge publication", cancellationToken);

    public Task SoftDeleteAsync(string entityType, DeleteKnowledgeEntityCommand command, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        var target = entityType switch
        {
            "CONCEPT_SCHEME" => ("knowledge.ConceptScheme", "ConceptSchemeId", true),
            "KNOWLEDGE_CONCEPT" => ("knowledge.KnowledgeConcept", "KnowledgeConceptId", true),
            "CONCEPT_LABEL" => ("knowledge.ConceptLabel", "ConceptLabelId", true),
            "CONCEPT_RELATIONSHIP" => ("knowledge.ConceptRelationship", "ConceptRelationshipId", true),
            "EXTERNAL_CONCEPT_MAPPING" => ("knowledge.ExternalConceptMapping", "ExternalConceptMappingId", false),
            "CONCEPT_VALIDATION_RULE" => ("knowledge.ConceptValidationRule", "ConceptValidationRuleId", true),
            "KNOWLEDGE_PUBLICATION" => ("knowledge.Publication", "PublicationId", true),
            "KNOWLEDGE_IMPORT" => ("knowledge.ImportJob", "ImportJobId", false),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unsupported Knowledge entity type.")
        };
        var systemGuard = target.Item3 ? " AND IsSystemDefined = 0" : string.Empty;
        var sql = $"UPDATE {target.Item1} SET IsDeleted = 1, ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE {target.Item2} = @EntityId AND TenantId = @ContextTenantId{systemGuard} AND IsDeleted = 0 AND RowVersion = @RowVersion;";
        return ExecuteAuditedMutationAsync(sql, command, audit, "Knowledge entity", cancellationToken);
    }

    public async Task PublishAsync(Guid contextTenantId, Guid publicationId, string publishedStatusCode, string changeReason, Guid actorUserId, byte[] expectedRowVersion, KnowledgeAuditFact audit, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM knowledge.PublicationItem WHERE PublicationId = @PublicationId)
    THROW 51002, 'A publication must contain at least one versioned item.', 1;

UPDATE knowledge.Publication
SET StatusCode = @PublishedStatusCode, PublishedByUserId = @ActorUserId, PublishedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE PublicationId = @PublicationId AND TenantId = @ContextTenantId AND IsDeleted = 0 AND PublishedDateUtc IS NULL AND RowVersion = @ExpectedRowVersion;
IF @@ROWCOUNT = 0 THROW 51003, 'The publication was changed, removed, or already published.', 1;
""";
        await ExecuteWriteAsync(sql, new { ContextTenantId = contextTenantId, PublicationId = publicationId, PublishedStatusCode = publishedStatusCode, ActorUserId = actorUserId, ExpectedRowVersion = expectedRowVersion }, audit, cancellationToken);
    }

    private async Task ExecuteAuditedMutationAsync(string sql, object parameters, KnowledgeAuditFact audit, string entityName, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            EnsureSingleRow(affected, entityName);
            await WriteAuditAndOutboxAsync(connection, transaction, audit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidateTenant(audit.TenantId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RelationshipPredicateBehavior> GetPredicateBehaviorAsync(string predicateCode, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IsHierarchical, SubjectIsChild FROM knowledge.RelationshipPredicate WHERE PredicateCode = @PredicateCode AND IsActive = 1;";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RelationshipPredicateBehavior>(new CommandDefinition(sql, new { PredicateCode = predicateCode }, cancellationToken: cancellationToken))
            ?? throw new KeyNotFoundException($"Relationship predicate '{predicateCode}' was not found or is inactive.");
    }

    public async Task<IReadOnlyCollection<(Guid ParentConceptId, Guid ChildConceptId)>> GetApprovedHierarchyEdgesAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ParentConceptId, KnowledgeConceptId AS ChildConceptId
FROM knowledge.KnowledgeConcept
WHERE ParentConceptId IS NOT NULL AND IsDeleted = 0 AND StatusCode IN (N'APPROVED', N'PUBLISHED') AND (TenantId IS NULL OR TenantId = @TenantId)
UNION
SELECT CASE WHEN predicate.SubjectIsChild = 1 THEN relation.ObjectConceptId ELSE relation.SubjectConceptId END,
       CASE WHEN predicate.SubjectIsChild = 1 THEN relation.SubjectConceptId ELSE relation.ObjectConceptId END
FROM knowledge.ConceptRelationship relation
INNER JOIN knowledge.RelationshipPredicate predicate ON predicate.PredicateCode = relation.PredicateCode
WHERE predicate.IsHierarchical = 1 AND predicate.IsActive = 1 AND relation.IsDeleted = 0
  AND relation.StatusCode IN (N'APPROVED', N'PUBLISHED') AND (relation.TenantId IS NULL OR relation.TenantId = @TenantId);
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HierarchyEdgeRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.Select(row => (row.ParentConceptId, row.ChildConceptId)).ToArray();
    }

    private async Task ExecuteWriteAsync(string sql, object parameters, KnowledgeAuditFact audit, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            await WriteAuditAndOutboxAsync(connection, transaction, audit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidateTenant(audit.TenantId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record ConceptLabelPersistenceRow(Guid ConceptLabelId, Guid KnowledgeConceptId, string Label, string LabelTypeCode, string LanguageCode, string? Source, bool IsSearchable, bool IsDeprecated, Guid? TenantId, bool IsSystemDefined, Guid CreatedByUserId, DateTime CreatedDateUtc);
    private sealed record HierarchyEdgeRow(Guid ParentConceptId, Guid ChildConceptId);
}
