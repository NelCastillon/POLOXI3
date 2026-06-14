using Ams.Application.Common.Dtos;
using Ams.Application.Features.AutomationJobs;

namespace Ams.Application.Abstractions.Persistence;

public interface IAutomationRuntimeRepository
{
    Task<IReadOnlyCollection<JobScheduleDto>> GetDueSchedulesAsync(DateTime dueUtc, int take, CancellationToken cancellationToken = default);
    Task<Guid> CreateScheduledJobRunAsync(CreateScheduledJobRunRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JobRunDto>> GetQueuedJobRunsAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> TryStartJobRunAsync(Guid jobRunId, StartJobRunRequest request, CancellationToken cancellationToken = default);
    Task CompleteJobRunAsync(Guid jobRunId, CompleteJobRunRequest request, CancellationToken cancellationToken = default);
    Task UpdateJobScheduleRunStateAsync(Guid jobScheduleId, UpdateJobScheduleRunStateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JobStepDto>> GetEnabledJobStepsAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateJobStepRunAsync(CreateJobStepRunRequest request, CancellationToken cancellationToken = default);
    Task StartJobStepRunAsync(Guid jobStepRunId, DateTime startedDateUtc, CancellationToken cancellationToken = default);
    Task CompleteJobStepRunAsync(Guid jobStepRunId, CompleteJobStepRunRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateFileExecutionLogAsync(CreateFileExecutionLogRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateFileRunLogAsync(CreateFileRunLogRequest request, CancellationToken cancellationToken = default);
}
