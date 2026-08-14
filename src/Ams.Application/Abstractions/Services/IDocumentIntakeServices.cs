using Ams.Application.Common.Models;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Application.Abstractions.Services;

public interface IDocumentIntakeService
{
    Task<PagedResult<DocumentIntakeSessionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? moduleCode, string? statusCode, Guid? assignedToUserId, Guid? targetEntityId = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>> GetDocumentStatusesAsync(Guid tenantId, string moduleCode, Guid targetEntityId, CancellationToken cancellationToken = default);
    Task<DocumentIntakeDetailDto?> GetAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDocumentIntakeSessionCommand command, CancellationToken cancellationToken = default);
    Task AttachDocumentAsync(AttachDocumentToIntakeCommand command, CancellationToken cancellationToken = default);
    Task QueueAsync(QueueDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task ReviewFieldAsync(ReviewDocumentIntakeFieldCommand command, CancellationToken cancellationToken = default);
    Task ResolveIssueAsync(ResolveDocumentIntakeIssueCommand command, CancellationToken cancellationToken = default);
    Task ReprocessAsync(ReprocessDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task CancelAsync(CancelDocumentIntakeCommand command, CancellationToken cancellationToken = default);
    Task<DocumentIntakePromotionResult> PromoteAsync(PromoteDocumentIntakeCommand command, CancellationToken cancellationToken = default);
}

public interface IDocumentSearchIndexer
{
    Task IndexAsync(DocumentIntakeProcessingContext context, CancellationToken cancellationToken = default);
}

public interface IDocumentOcrProvider
{
    Task<DocumentOcrResult> AnalyzeAsync(DocumentOcrRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentInterpretationProvider
{
    Task<DocumentInterpretationResult> InterpretAsync(DocumentInterpretationRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentKnowledgeNormalizer
{
    Task<IReadOnlyCollection<KnowledgeNormalizedField>> NormalizeAsync(KnowledgeNormalizationRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentIntakePayloadStore
{
    Task<string> SaveJsonAsync(Guid tenantId, Guid intakeSessionId, string payloadType, string json, CancellationToken cancellationToken = default);
    Task<string> ReadJsonAsync(Guid tenantId, Guid? intakeSessionId, string storageReference, CancellationToken cancellationToken = default);
}

public sealed record DocumentIntakePromotionResult(Guid IntakeSessionId, Guid TargetEntityId, string ModuleCode, bool AlreadyPromoted, string Message);
