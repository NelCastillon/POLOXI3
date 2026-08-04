using System.Text.Json;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Common.Validation;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Domain.Common;
using Ams.Knowledge.Domain.Concepts;
using Ams.Knowledge.Domain.Governance;
using Ams.Knowledge.Domain.Mappings;

namespace Ams.Knowledge.Application.Services;

public interface IKnowledgeAdministrationService
{
    Task<Guid> CreateSchemeAsync(CreateConceptSchemeCommand command, CancellationToken cancellationToken = default);
    Task UpdateSchemeAsync(UpdateConceptSchemeCommand command, CancellationToken cancellationToken = default);
    Task<Guid> CreateConceptAsync(CreateKnowledgeConceptCommand command, CancellationToken cancellationToken = default);
    Task UpdateConceptDraftAsync(UpdateKnowledgeConceptDraftCommand command, CancellationToken cancellationToken = default);
    Task<Guid> AddLabelAsync(AddConceptLabelCommand command, CancellationToken cancellationToken = default);
    Task UpdateLabelAsync(UpdateConceptLabelCommand command, CancellationToken cancellationToken = default);
    Task<Guid> AddRelationshipAsync(AddConceptRelationshipCommand command, CancellationToken cancellationToken = default);
    Task UpdateRelationshipAsync(UpdateConceptRelationshipCommand command, CancellationToken cancellationToken = default);
    Task<Guid> CreateMappingAsync(CreateExternalMappingCommand command, CancellationToken cancellationToken = default);
    Task UpdateMappingAsync(UpdateExternalMappingCommand command, CancellationToken cancellationToken = default);
    Task ReviewMappingAsync(ReviewExternalMappingCommand command, CancellationToken cancellationToken = default);
    Task<Guid> QueueImportAsync(QueueKnowledgeImportCommand command, CancellationToken cancellationToken = default);
    Task UpdateImportAsync(UpdateKnowledgeImportCommand command, CancellationToken cancellationToken = default);
    Task<Guid> CreateValidationRuleAsync(CreateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default);
    Task UpdateValidationRuleAsync(UpdateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default);
    Task<Guid> CreatePublicationAsync(CreateKnowledgePublicationCommand command, CancellationToken cancellationToken = default);
    Task UpdatePublicationAsync(UpdateKnowledgePublicationCommand command, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(string entityType, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default);
    Task PublishAsync(PublishKnowledgeCommand command, CancellationToken cancellationToken = default);
}

public sealed class KnowledgeAdministrationService : IKnowledgeAdministrationService
{
    private readonly IKnowledgeCommandRepository _repository;

    public KnowledgeAdministrationService(IKnowledgeCommandRepository repository) => _repository = repository;

    public Task<Guid> CreateSchemeAsync(CreateConceptSchemeCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        var now = DateTime.UtcNow;
        var scheme = new ConceptScheme(Guid.NewGuid(), command.SchemeCode, command.Name, command.Description, command.AuthorityCode, command.VersionLabel, command.StatusCode, command.TenantId, command.IsSystemDefined, command.ActorUserId, now);
        return _repository.CreateSchemeAsync(scheme, Audit(command.ContextTenantId, command.ActorUserId, "SCHEME_CREATED", "CONCEPT_SCHEME", scheme.Id, null, scheme, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task UpdateSchemeAsync(UpdateConceptSchemeCommand command, CancellationToken cancellationToken = default)
        => UpdateAsync(command, command.ContextTenantId, command.ActorUserId, "SCHEME_UPDATED", "CONCEPT_SCHEME", command.ConceptSchemeId, command.ChangeReason, command.CorrelationId, audit => _repository.UpdateSchemeAsync(command, audit, cancellationToken));

    public async Task<Guid> CreateConceptAsync(CreateKnowledgeConceptCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        await _repository.EnsureSchemeAccessibleAsync(command.ContextTenantId, command.ConceptSchemeId, cancellationToken);
        if (command.ParentConceptId.HasValue)
            await _repository.EnsureConceptsAccessibleAsync(command.ContextTenantId, [command.ParentConceptId.Value], cancellationToken);
        var now = DateTime.UtcNow;
        var concept = new KnowledgeConcept(Guid.NewGuid(), command.ConceptSchemeId, command.ConceptCode, command.ConceptTypeCode, command.PreferredLabel, command.Definition, command.ParentConceptId, command.IsAbstract, command.IsSelectable, command.StatusCode, command.EffectiveFromUtc, command.EffectiveToUtc, 1, null, command.TenantId, command.IsSystemDefined, command.OwnerUserId, command.BusinessStewardUserId, command.TechnicalStewardUserId, command.DefinitionSource, command.LicensingNotes, command.ActorUserId, now);
        return await _repository.CreateConceptAsync(concept, Audit(command.ContextTenantId, command.ActorUserId, "CONCEPT_CREATED", "KNOWLEDGE_CONCEPT", concept.Id, null, concept, command.ChangeReason, command.CorrelationId, concept.VersionNumber, now), cancellationToken);
    }

    public async Task UpdateConceptDraftAsync(UpdateKnowledgeConceptDraftCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        var concept = await _repository.GetConceptAggregateAsync(command.ContextTenantId, command.KnowledgeConceptId, cancellationToken)
            ?? throw new KeyNotFoundException("The Knowledge concept was not found.");
        if (command.ParentConceptId.HasValue)
            await _repository.EnsureConceptsAccessibleAsync(command.ContextTenantId, [command.ParentConceptId.Value], cancellationToken);
        var oldValue = JsonSerializer.Serialize(concept);
        var now = DateTime.UtcNow;
        concept.ReviseDraft(command.ConceptTypeCode, command.PreferredLabel, command.Definition, command.ParentConceptId, command.IsAbstract, command.IsSelectable, command.EffectiveFromUtc, command.EffectiveToUtc, command.OwnerUserId, command.BusinessStewardUserId, command.TechnicalStewardUserId, command.DefinitionSource, command.LicensingNotes, command.ActorUserId, now);
        await _repository.UpdateConceptAsync(concept, command.RowVersion, Audit(command.ContextTenantId, command.ActorUserId, "CONCEPT_REVISED", "KNOWLEDGE_CONCEPT", concept.Id, oldValue, concept, command.ChangeReason, command.CorrelationId, concept.VersionNumber, now), cancellationToken);
    }

    public async Task<Guid> AddLabelAsync(AddConceptLabelCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        var concept = await _repository.GetConceptAggregateAsync(command.ContextTenantId, command.KnowledgeConceptId, cancellationToken)
            ?? throw new KeyNotFoundException("The Knowledge concept was not found.");
        var now = DateTime.UtcNow;
        var label = new ConceptLabel(Guid.NewGuid(), command.KnowledgeConceptId, command.Label, command.LabelTypeCode, command.LanguageCode, command.Source, command.IsSearchable, false, command.TenantId, command.IsSystemDefined, command.ActorUserId, now);
        concept.AddLabel(label);
        return await _repository.AddLabelAsync(label, Audit(command.ContextTenantId, command.ActorUserId, "LABEL_ADDED", "CONCEPT_LABEL", label.Id, null, label, command.ChangeReason, command.CorrelationId, concept.VersionNumber, now), cancellationToken);
    }

    public Task UpdateLabelAsync(UpdateConceptLabelCommand command, CancellationToken cancellationToken = default)
        => UpdateAsync(command, command.ContextTenantId, command.ActorUserId, "LABEL_UPDATED", "CONCEPT_LABEL", command.ConceptLabelId, command.ChangeReason, command.CorrelationId, audit => _repository.UpdateLabelAsync(command, audit, cancellationToken));

    public async Task<Guid> AddRelationshipAsync(AddConceptRelationshipCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        await _repository.EnsureConceptsAccessibleAsync(command.ContextTenantId, [command.SubjectConceptId, command.ObjectConceptId], cancellationToken);
        var predicate = await _repository.GetPredicateBehaviorAsync(command.PredicateCode, command.TenantId, cancellationToken);
        if (predicate.IsHierarchical)
        {
            var edges = await _repository.GetApprovedHierarchyEdgesAsync(command.TenantId, cancellationToken);
            var childConceptId = predicate.SubjectIsChild ? command.SubjectConceptId : command.ObjectConceptId;
            var parentConceptId = predicate.SubjectIsChild ? command.ObjectConceptId : command.SubjectConceptId;
            ConceptHierarchy.EnsureCanAddParent(childConceptId, parentConceptId, edges);
        }
        var now = DateTime.UtcNow;
        var relationship = new ConceptRelationship(Guid.NewGuid(), command.SubjectConceptId, command.PredicateCode, command.ObjectConceptId, command.RelationshipStrength, command.Source, command.EffectiveFromUtc, command.EffectiveToUtc, command.StatusCode, command.TenantId, command.IsSystemDefined, command.ActorUserId, now);
        return await _repository.AddRelationshipAsync(relationship, Audit(command.ContextTenantId, command.ActorUserId, "RELATIONSHIP_ADDED", "CONCEPT_RELATIONSHIP", relationship.Id, null, relationship, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task UpdateRelationshipAsync(UpdateConceptRelationshipCommand command, CancellationToken cancellationToken = default)
        => UpdateAsync(command, command.ContextTenantId, command.ActorUserId, "RELATIONSHIP_UPDATED", "CONCEPT_RELATIONSHIP", command.ConceptRelationshipId, command.ChangeReason, command.CorrelationId, audit => _repository.UpdateRelationshipAsync(command, audit, cancellationToken));

    public async Task<Guid> CreateMappingAsync(CreateExternalMappingCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.TenantId);
        await EnsureMappingConceptsAsync(command.TenantId, command.KnowledgeConceptId, command.LineOfBusinessConceptId, cancellationToken);
        var now = DateTime.UtcNow;
        var mapping = new ExternalConceptMapping(Guid.NewGuid(), command.KnowledgeConceptId, command.SourceSystemTypeCode, command.SourceSystemId, command.ExternalCode, command.ExternalValue, command.ExternalPath, command.MappingDirectionCode, command.MatchTypeCode, command.ConfidenceScore, command.StateCode, command.LineOfBusinessConceptId, command.CarrierProductId, command.EffectiveFromUtc, command.EffectiveToUtc, command.TenantId, false, command.ActorUserId, now);
        var review = new MappingReview(Guid.NewGuid(), mapping.Id, command.InitialReviewStatusCode, null, command.TenantId, command.ActorUserId, now);
        return await _repository.CreateMappingAsync(mapping, review, Audit(command.TenantId, command.ActorUserId, "MAPPING_CREATED", "EXTERNAL_CONCEPT_MAPPING", mapping.Id, null, mapping, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public async Task UpdateMappingAsync(UpdateExternalMappingCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.TenantId);
        await EnsureMappingConceptsAsync(command.TenantId, command.KnowledgeConceptId, command.LineOfBusinessConceptId, cancellationToken);
        var now = DateTime.UtcNow;
        await _repository.UpdateMappingAsync(command, Audit(command.TenantId, command.ActorUserId, "MAPPING_UPDATED", "EXTERNAL_CONCEPT_MAPPING", command.ExternalConceptMappingId, null, command, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task ReviewMappingAsync(ReviewExternalMappingCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.TenantId);
        var now = DateTime.UtcNow;
        return _repository.ReviewMappingAsync(command.TenantId, command.MappingReviewId, command.ExternalConceptMappingId, command.DecisionStatusCode, command.Reason, command.ReviewerUserId, command.RowVersion, Audit(command.TenantId, command.ReviewerUserId, "MAPPING_REVIEWED", "EXTERNAL_CONCEPT_MAPPING", command.ExternalConceptMappingId, null, new { command.DecisionStatusCode }, command.Reason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task<Guid> QueueImportAsync(QueueKnowledgeImportCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.TenantId);
        ValidateStorageReference(command.StorageReference);
        var now = DateTime.UtcNow;
        var import = new KnowledgeImportJob(Guid.NewGuid(), command.TenantId, command.ImportTypeCode, command.SourceFileName, command.StorageReference, command.InitialStatusCode, command.CorrelationId, command.ActorUserId, now);
        return _repository.QueueImportAsync(import, Audit(command.TenantId, command.ActorUserId, "IMPORT_QUEUED", "KNOWLEDGE_IMPORT", import.Id, null, import, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task UpdateImportAsync(UpdateKnowledgeImportCommand command, CancellationToken cancellationToken = default)
    {
        ValidateStorageReference(command.StorageReference);
        return UpdateAsync(command, command.TenantId, command.ActorUserId, "IMPORT_UPDATED", "KNOWLEDGE_IMPORT", command.ImportJobId, command.ChangeReason, command.CorrelationId, audit => _repository.UpdateImportAsync(command, audit, cancellationToken));
    }

    public async Task<Guid> CreateValidationRuleAsync(CreateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        await _repository.EnsureConceptsAccessibleAsync(command.ContextTenantId, [command.AppliesToConceptId], cancellationToken);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return await _repository.CreateValidationRuleAsync(command, Audit(command.ContextTenantId, command.ActorUserId, "RULE_CREATED", "CONCEPT_VALIDATION_RULE", id, null, command, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public async Task UpdateValidationRuleAsync(UpdateKnowledgeValidationRuleCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        await _repository.EnsureConceptsAccessibleAsync(command.ContextTenantId, [command.AppliesToConceptId], cancellationToken);
        var now = DateTime.UtcNow;
        await _repository.UpdateValidationRuleAsync(command, Audit(command.ContextTenantId, command.ActorUserId, "RULE_UPDATED", "CONCEPT_VALIDATION_RULE", command.ConceptValidationRuleId, null, command, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task<Guid> CreatePublicationAsync(CreateKnowledgePublicationCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return _repository.CreatePublicationAsync(command, Audit(command.ContextTenantId, command.ActorUserId, "PUBLICATION_CREATED", "KNOWLEDGE_PUBLICATION", id, null, command, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    public Task UpdatePublicationAsync(UpdateKnowledgePublicationCommand command, CancellationToken cancellationToken = default)
        => UpdateAsync(command, command.ContextTenantId, command.ActorUserId, "PUBLICATION_UPDATED", "KNOWLEDGE_PUBLICATION", command.PublicationId, command.ChangeReason, command.CorrelationId, audit => _repository.UpdatePublicationAsync(command, audit, cancellationToken));

    public Task SoftDeleteAsync(string entityType, DeleteKnowledgeEntityCommand command, CancellationToken cancellationToken = default)
        => UpdateAsync(command, command.ContextTenantId, command.ActorUserId, "ENTITY_DELETED", entityType, command.EntityId, command.ChangeReason, command.CorrelationId, audit => _repository.SoftDeleteAsync(entityType, command, audit, cancellationToken));

    public Task PublishAsync(PublishKnowledgeCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command, command.ContextTenantId);
        var now = DateTime.UtcNow;
        return _repository.PublishAsync(command.ContextTenantId, command.PublicationId, command.PublishedStatusCode, command.ChangeReason, command.ActorUserId, command.RowVersion, Audit(command.ContextTenantId, command.ActorUserId, "KNOWLEDGE_PUBLISHED", "KNOWLEDGE_PUBLICATION", command.PublicationId, null, new { command.PublishedStatusCode }, command.ChangeReason, command.CorrelationId, null, now), cancellationToken);
    }

    private async Task EnsureMappingConceptsAsync(Guid tenantId, Guid conceptId, Guid? lineOfBusinessConceptId, CancellationToken cancellationToken)
    {
        var conceptIds = new[] { (Guid?)conceptId, lineOfBusinessConceptId }.Where(id => id.HasValue).Select(id => id!.Value).ToArray();
        await _repository.EnsureConceptsAccessibleAsync(tenantId, conceptIds, cancellationToken);
    }

    private static void ValidateStorageReference(string storageReference)
    {
        if (Path.IsPathRooted(storageReference) || storageReference.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
            throw new ApplicationValidationException(["StorageReference must be a relative path within the configured Knowledge import root."]);
    }

    private static void Validate(object command, Guid tenantId)
    {
        RequestValidator.Validate(command);
        if (tenantId == Guid.Empty)
            throw new ApplicationValidationException(["The tenant context is required."]);
    }

    private static Task UpdateAsync(object command, Guid tenantId, Guid actorUserId, string actionCode, string entityType, Guid entityId, string reason, string correlationId, Func<KnowledgeAuditFact, Task> update)
    {
        Validate(command, tenantId);
        var now = DateTime.UtcNow;
        return update(Audit(tenantId, actorUserId, actionCode, entityType, entityId, null, command, reason, correlationId, null, now));
    }

    private static KnowledgeAuditFact Audit(Guid tenantId, Guid actorUserId, string actionCode, string entityTypeCode, Guid entityId, object? oldValue, object? newValue, string reason, string correlationId, int? versionNumber, DateTime occurredUtc)
        => new(tenantId, actorUserId, actionCode, entityTypeCode, entityId, oldValue as string ?? (oldValue is null ? null : JsonSerializer.Serialize(oldValue)), newValue is null ? null : JsonSerializer.Serialize(newValue), reason, "KNOWLEDGE_APPLICATION", correlationId, versionNumber, occurredUtc);
}
