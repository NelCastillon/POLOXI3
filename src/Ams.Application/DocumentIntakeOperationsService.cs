using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Application;

public sealed class DocumentIntakeOperationsService(IDocumentIntakeOperationsRepository repository):IDocumentIntakeOperationsService
{
    public Task<DocumentIntakeRuntimeSettings> GetSettingsAsync(Guid? tenantId,CancellationToken cancellationToken=default)=>repository.GetSettingsAsync(tenantId,cancellationToken);
    public Task<IReadOnlyCollection<DocumentIntakeDeadLetterDto>> GetDeadLettersAsync(Guid tenantId,int pageSize=100,CancellationToken cancellationToken=default)=>repository.GetDeadLettersAsync(tenantId,Math.Clamp(pageSize,1,500),cancellationToken);
    public Task ReplayDeadLetterAsync(ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken=default){DocumentIntakeValidator.Validate(command);return repository.ReplayDeadLetterAsync(command,cancellationToken);}
    public Task PlaceLegalHoldAsync(PlaceDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default){DocumentIntakeValidator.Validate(command);return repository.PlaceLegalHoldAsync(command,cancellationToken);}
    public Task ReleaseLegalHoldAsync(ReleaseDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken=default){DocumentIntakeValidator.Validate(command);return repository.ReleaseLegalHoldAsync(command,cancellationToken);}
    public Task<IReadOnlyCollection<DocumentIntakePromptSuiteDto>> GetPromptSuitesAsync(Guid? tenantId,CancellationToken cancellationToken=default)=>repository.GetPromptSuitesAsync(tenantId,cancellationToken);
    public Task<IReadOnlyCollection<DocumentIntakePromptEvaluationRunDto>> GetPromptEvaluationRunsAsync(Guid? tenantId,int pageSize=100,CancellationToken cancellationToken=default)=>repository.GetPromptEvaluationRunsAsync(tenantId,Math.Clamp(pageSize,1,500),cancellationToken);
    public Task<Guid> QueuePromptEvaluationAsync(QueuePromptEvaluationCommand command,CancellationToken cancellationToken=default){DocumentIntakeValidator.Validate(command);return repository.QueuePromptEvaluationAsync(command,cancellationToken);}
    public async Task ApprovePromptAsync(ApproveDocumentIntakePromptCommand command,CancellationToken cancellationToken=default){DocumentIntakeValidator.Validate(command);var settings=await repository.GetSettingsAsync(command.TenantId,cancellationToken);await repository.ApprovePromptAsync(command,settings.RequirePassedPromptEvaluation,cancellationToken);}
    public Task<IReadOnlyCollection<DocumentIntakeAlertDto>> GetAlertsAsync(Guid? tenantId,bool openOnly=true,CancellationToken cancellationToken=default)=>repository.GetAlertsAsync(tenantId,openOnly,cancellationToken);
}
