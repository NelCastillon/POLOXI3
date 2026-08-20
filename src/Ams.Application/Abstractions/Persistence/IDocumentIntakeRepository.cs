using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentIntakeRepository
{
    Task<PagedResult<DocumentIntakeSessionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? moduleCode, string? statusCode, Guid? assignedToUserId, Guid? targetEntityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>> GetDocumentStatusesAsync(Guid tenantId, string moduleCode, Guid targetEntityId, CancellationToken cancellationToken = default);
    Task<DocumentIntakeDetailDto?> GetAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateSessionAsync(CreateDocumentIntakeSessionCommand command, CancellationToken cancellationToken = default);
    Task AttachDocumentAsync(AttachDocumentToIntakeCommand command, CancellationToken cancellationToken = default);
    Task QueueAsync(QueueDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task ReviewFieldAsync(ReviewDocumentIntakeFieldCommand command, CancellationToken cancellationToken = default);
    Task ResolveIssueAsync(ResolveDocumentIntakeIssueCommand command, CancellationToken cancellationToken = default);
    Task ReprocessAsync(ReprocessDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task CancelAsync(CancelDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task<SubmissionIntakeDraft> BuildReviewedSubmissionDraftAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default);
    Task<DocumentIntakePromotionConfigurationDto?> GetPromotionConfigurationAsync(Guid tenantId, string moduleCode, CancellationToken cancellationToken = default);
    Task<DocumentIntakePromotionRecord?> GetPromotionAsync(Guid tenantId, Guid intakeSessionId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<DocumentIntakePromotionStart> BeginPromotionAsync(PromoteDocumentIntakeCommand command, string requestJson, CancellationToken cancellationToken = default);
    Task UpdatePromotionProgressAsync(Guid tenantId, Guid promotionId, Guid? submissionIntakeId, Guid? accountId, Guid? opportunityId, Guid? lobId, string? errorMessage, CancellationToken cancellationToken = default);
    Task LinkDocumentsToSubmissionAsync(Guid tenantId, Guid intakeSessionId, Guid promotionId, Guid submissionId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task CompletePromotionAsync(Guid tenantId, Guid intakeSessionId, Guid promotionId, Guid targetEntityId, string resultJson, Guid actorUserId, byte[] expectedSessionRowVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentIntakeWorkItemDto>> LeaseWorkItemsAsync(string leaseOwner, int batchSize, TimeSpan leaseDuration, bool malwareEnabled = true, bool malwareFailClosed = true, CancellationToken cancellationToken = default);
    Task<DocumentIntakeProcessingContext?> GetProcessingContextAsync(Guid workItemId, string leaseOwner, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ExtractedDocumentField>> GetExtractedFieldsAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default);
    Task ValidateDraftAsync(DocumentIntakeProcessingContext context, CancellationToken cancellationToken = default);
    Task SaveOcrResultAsync(DocumentIntakeProcessingContext context, DocumentOcrResult result, string outputReference, string inputHash, string outputHash, CancellationToken cancellationToken = default);
    Task SaveInterpretationAsync(DocumentIntakeProcessingContext context, DocumentInterpretationResult result, string outputReference, string inputHash, string outputHash, CancellationToken cancellationToken = default);
    Task SaveNormalizedFieldsAsync(DocumentIntakeProcessingContext context, IReadOnlyCollection<KnowledgeNormalizedField> fields, CancellationToken cancellationToken = default);
    Task CompleteWorkItemAsync(Guid workItemId, string leaseOwner, CancellationToken cancellationToken = default);
    Task FailWorkItemAsync(Guid workItemId, string leaseOwner, string errorCode, string errorMessage, bool retryable, CancellationToken cancellationToken = default);
}

public sealed record DocumentIntakePromotionRecord(Guid IntakePromotionId, string StatusCode, Guid? TargetEntityId, string? ResultJson, Guid? SubmissionIntakeId, Guid? AccountId, Guid? OpportunityId, Guid? LobId, string? LastErrorMessage);
public sealed record DocumentIntakePromotionStart(Guid IntakePromotionId, bool Created);

public sealed record DocumentIntakeProcessingContext(
    DocumentIntakeWorkItemDto WorkItem,
    DocumentIntakeSessionDto Session,
    DocumentIntakeDocumentDto? Document,
    string? StoragePath,
    string? OcrOutputReference,
    string? OcrJson,
    string? PromptCode,
    string? PromptVersion,
    string? SystemPrompt,
    string? OutputSchemaJson);
