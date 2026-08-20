using System.ComponentModel.DataAnnotations;

namespace Ams.Knowledge.Application.Features.Knowledge;

public sealed record CreateConceptSchemeCommand(
    [Required] Guid ContextTenantId,
    [Required, StringLength(100)] string SchemeCode,
    [Required, StringLength(200)] string Name,
    string? Description,
    [Required, StringLength(100)] string AuthorityCode,
    [StringLength(50)] string? VersionLabel,
    [Required, StringLength(30)] string StatusCode,
    Guid? TenantId,
    bool IsSystemDefined,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateConceptSchemeCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid ConceptSchemeId,
    [Required, StringLength(200)] string Name,
    [StringLength(4000)] string? Description,
    [Required, StringLength(100)] string AuthorityCode,
    [StringLength(50)] string? VersionLabel,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record CreateKnowledgeConceptCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid ConceptSchemeId,
    [Required, StringLength(100)] string ConceptCode,
    [Required, StringLength(50)] string ConceptTypeCode,
    [Required, StringLength(250)] string PreferredLabel,
    string? Definition,
    Guid? ParentConceptId,
    bool IsAbstract,
    bool IsSelectable,
    [Required, StringLength(30)] string StatusCode,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    Guid? TenantId,
    bool IsSystemDefined,
    [Required] Guid OwnerUserId,
    [Required] Guid BusinessStewardUserId,
    Guid? TechnicalStewardUserId,
    [Required, StringLength(500)] string DefinitionSource,
    [StringLength(2000)] string? LicensingNotes,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateConceptLabelCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid ConceptLabelId,
    [Required, StringLength(250)] string Label,
    [Required, StringLength(30)] string LabelTypeCode,
    [Required, StringLength(10)] string LanguageCode,
    [StringLength(100)] string? Source,
    bool IsSearchable,
    bool IsDeprecated,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record UpdateKnowledgeConceptDraftCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid KnowledgeConceptId,
    [Required, StringLength(50)] string ConceptTypeCode,
    [Required, StringLength(250)] string PreferredLabel,
    string? Definition,
    Guid? ParentConceptId,
    bool IsAbstract,
    bool IsSelectable,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required] Guid OwnerUserId,
    [Required] Guid BusinessStewardUserId,
    Guid? TechnicalStewardUserId,
    [Required, StringLength(500)] string DefinitionSource,
    [StringLength(2000)] string? LicensingNotes,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record AddConceptLabelCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid KnowledgeConceptId,
    [Required, StringLength(250)] string Label,
    [Required, StringLength(30)] string LabelTypeCode,
    [Required, StringLength(10)] string LanguageCode,
    [StringLength(100)] string? Source,
    bool IsSearchable,
    Guid? TenantId,
    bool IsSystemDefined,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateConceptRelationshipCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid ConceptRelationshipId,
    [Required, StringLength(100)] string PredicateCode,
    [Range(typeof(decimal), "0", "1")] decimal? RelationshipStrength,
    [StringLength(100)] string? Source,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record AddConceptRelationshipCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid SubjectConceptId,
    [Required, StringLength(100)] string PredicateCode,
    [Required] Guid ObjectConceptId,
    [Range(typeof(decimal), "0", "1")] decimal? RelationshipStrength,
    [StringLength(100)] string? Source,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(30)] string StatusCode,
    Guid? TenantId,
    bool IsSystemDefined,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateExternalMappingCommand(
    [Required] Guid TenantId,
    [Required] Guid ExternalConceptMappingId,
    [Required] Guid KnowledgeConceptId,
    [Required, StringLength(50)] string SourceSystemTypeCode,
    Guid? SourceSystemId,
    [StringLength(150)] string? ExternalCode,
    [Required, StringLength(500)] string ExternalValue,
    [StringLength(500)] string? ExternalPath,
    [Required, StringLength(20)] string MappingDirectionCode,
    [Required, StringLength(30)] string MatchTypeCode,
    [Range(typeof(decimal), "0", "1")] decimal? ConfidenceScore,
    [StringLength(2, MinimumLength = 2)] string? StateCode,
    Guid? LineOfBusinessConceptId,
    Guid? CarrierProductId,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record CreateKnowledgeValidationRuleCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid AppliesToConceptId,
    [Required, StringLength(100)] string RuleCode,
    [Required, StringLength(50)] string RuleTypeCode,
    [StringLength(500)] string? PropertyPath,
    [Required, StringLength(50)] string OperatorCode,
    string? ExpectedValue,
    [Range(0, int.MaxValue)] int? MinimumCount,
    [Range(0, int.MaxValue)] int? MaximumCount,
    [Required, StringLength(30)] string SeverityCode,
    [Required, StringLength(1000)] string Message,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateKnowledgeValidationRuleCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid ConceptValidationRuleId,
    [Required] Guid AppliesToConceptId,
    [Required, StringLength(50)] string RuleTypeCode,
    [StringLength(500)] string? PropertyPath,
    [Required, StringLength(50)] string OperatorCode,
    string? ExpectedValue,
    [Range(0, int.MaxValue)] int? MinimumCount,
    [Range(0, int.MaxValue)] int? MaximumCount,
    [Required, StringLength(30)] string SeverityCode,
    [Required, StringLength(1000)] string Message,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record CreateKnowledgePublicationCommand(
    [Required] Guid ContextTenantId,
    [Required, StringLength(100)] string PublicationCode,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(50)] string VersionLabel,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record UpdateKnowledgePublicationCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid PublicationId,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(50)] string VersionLabel,
    [Required, StringLength(30)] string StatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record UpdateKnowledgeImportCommand(
    [Required] Guid TenantId,
    [Required] Guid ImportJobId,
    [Required, StringLength(50)] string ImportTypeCode,
    [Required, StringLength(260)] string SourceFileName,
    [Required, StringLength(1000)] string StorageReference,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record DeleteKnowledgeEntityCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid EntityId,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record CreateExternalMappingCommand(
    [Required] Guid TenantId,
    [Required] Guid KnowledgeConceptId,
    [Required, StringLength(50)] string SourceSystemTypeCode,
    Guid? SourceSystemId,
    [StringLength(150)] string? ExternalCode,
    [Required, StringLength(500)] string ExternalValue,
    [StringLength(500)] string? ExternalPath,
    [Required, StringLength(20)] string MappingDirectionCode,
    [Required, StringLength(30)] string MatchTypeCode,
    [Required, StringLength(30)] string InitialReviewStatusCode,
    [Range(typeof(decimal), "0", "1")] decimal? ConfidenceScore,
    [StringLength(2, MinimumLength = 2)] string? StateCode,
    Guid? LineOfBusinessConceptId,
    Guid? CarrierProductId,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId);

public sealed record ReviewExternalMappingCommand(
    [Required] Guid TenantId,
    [Required] Guid MappingReviewId,
    [Required] Guid ExternalConceptMappingId,
    [Required, StringLength(30)] string DecisionStatusCode,
    [Required, StringLength(1000)] string Reason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ReviewerUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record QueueKnowledgeImportCommand(
    [Required] Guid TenantId,
    [Required, StringLength(50)] string ImportTypeCode,
    [Required, StringLength(30)] string InitialStatusCode,
    [Required, StringLength(260)] string SourceFileName,
    [Required, StringLength(1000)] string StorageReference,
    [Required, StringLength(120)] string CorrelationId,
    [Required, StringLength(1000)] string ChangeReason,
    [Required] Guid ActorUserId);

public sealed record PublishKnowledgeCommand(
    [Required] Guid ContextTenantId,
    [Required] Guid PublicationId,
    [Required, StringLength(30)] string PublishedStatusCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);
