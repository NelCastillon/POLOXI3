using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AutomationJobs;

namespace Ams.Application.Abstractions.Services;

public interface IAutomationJobService
{
    Task<AutomationSchedulerDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<JobDefinitionDto>> SearchJobDefinitionsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? categoryCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<JobDefinitionDto?> GetJobDefinitionAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JobStepDto>> GetJobStepsAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JobScheduleDto>> GetJobSchedulesAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
    Task<PagedResult<JobRunDto>> SearchJobRunsAsync(Guid tenantId, Guid? jobDefinitionId = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JobStepRunDto>> GetJobStepRunsAsync(Guid jobRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FileSaveDto>> GetFileSavesAsync(Guid jobRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FileExecutionLogDto>> GetFileExecutionLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FileRunLogDto>> GetFileRunLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default);
    Task<Guid> CreateJobDefinitionAsync(CreateJobDefinitionRequest request, CancellationToken cancellationToken = default);
    Task UpdateJobDefinitionAsync(Guid jobDefinitionId, UpdateJobDefinitionRequest request, CancellationToken cancellationToken = default);
    Task SetJobDefinitionStatusAsync(Guid jobDefinitionId, SetJobDefinitionStatusRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertJobStepAsync(Guid? jobStepId, UpsertJobStepRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertJobScheduleAsync(Guid? jobScheduleId, UpsertJobScheduleRequest request, CancellationToken cancellationToken = default);
    Task SetJobScheduleEnabledAsync(Guid jobScheduleId, SetJobScheduleEnabledRequest request, CancellationToken cancellationToken = default);
    Task<Guid> TriggerJobRunAsync(Guid jobDefinitionId, TriggerJobRunRequest request, CancellationToken cancellationToken = default);
}
