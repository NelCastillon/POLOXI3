using Ams.Application.Features.DocumentIntake;

namespace Ams.Application.Abstractions.Services;

public interface IDocumentIntakeOperationsService
{
    Task<DocumentIntakeRuntimeSettings> GetSettingsAsync(Guid? tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakeDeadLetterDto>> GetDeadLettersAsync(Guid tenantId,int pageSize=100,CancellationToken cancellationToken=default);
    Task ReplayDeadLetterAsync(ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken=default);
    Task PlaceLegalHoldAsync(PlaceDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default);
    Task ReleaseLegalHoldAsync(ReleaseDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePromptSuiteDto>> GetPromptSuitesAsync(Guid? tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>> GetPromptEvaluationRunsAsync(Guid? tenantId,int pageSize=100,CancellationToken cancellationToken=default);
    Task<Guid> QueuePromptEvaluationAsync(QueuePromptEvaluationCommand command,CancellationToken cancellationToken=default);
    Task ApprovePromptAsync(ApproveDocumentIntakePromptCommand command,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<DocumentIntakeAlertDto>> GetAlertsAsync(Guid? tenantId,bool openOnly=true,CancellationToken cancellationToken=default);
}
