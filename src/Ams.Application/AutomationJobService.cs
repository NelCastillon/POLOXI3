using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AutomationJobs;

namespace Ams.Application;

public sealed class AutomationJobService : IAutomationJobService
{
    private readonly IAutomationJobRepository _repository;

    public AutomationJobService(IAutomationJobRepository repository) => _repository = repository;

    public Task<AutomationSchedulerDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetDashboardAsync(tenantId, cancellationToken);

    public Task<PagedResult<JobDefinitionDto>> SearchJobDefinitionsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? categoryCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchJobDefinitionsAsync(tenantId, searchTerm, statusCode, categoryCode, pageNumber, pageSize, cancellationToken);

    public Task<JobDefinitionDto?> GetJobDefinitionAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetJobDefinitionAsync(jobDefinitionId, cancellationToken);

    public Task<IReadOnlyCollection<JobStepDto>> GetJobStepsAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetJobStepsAsync(jobDefinitionId, cancellationToken);

    public Task<IReadOnlyCollection<JobScheduleDto>> GetJobSchedulesAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetJobSchedulesAsync(jobDefinitionId, cancellationToken);

    public Task<PagedResult<JobRunDto>> SearchJobRunsAsync(Guid tenantId, Guid? jobDefinitionId = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchJobRunsAsync(tenantId, jobDefinitionId, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyCollection<JobStepRunDto>> GetJobStepRunsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
        => _repository.GetJobStepRunsAsync(jobRunId, cancellationToken);

    public Task<IReadOnlyCollection<FileSaveDto>> GetFileSavesAsync(Guid jobRunId, CancellationToken cancellationToken = default)
        => _repository.GetFileSavesAsync(jobRunId, cancellationToken);

    public Task<IReadOnlyCollection<FileExecutionLogDto>> GetFileExecutionLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
        => _repository.GetFileExecutionLogsAsync(jobRunId, cancellationToken);

    public Task<IReadOnlyCollection<FileRunLogDto>> GetFileRunLogsAsync(Guid jobRunId, CancellationToken cancellationToken = default)
        => _repository.GetFileRunLogsAsync(jobRunId, cancellationToken);

    public Task<Guid> CreateJobDefinitionAsync(CreateJobDefinitionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateJobDefinitionAsync(request, cancellationToken);

    public Task UpdateJobDefinitionAsync(Guid jobDefinitionId, UpdateJobDefinitionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateJobDefinitionAsync(jobDefinitionId, request, cancellationToken);

    public Task SetJobDefinitionStatusAsync(Guid jobDefinitionId, SetJobDefinitionStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.SetJobDefinitionStatusAsync(jobDefinitionId, request, cancellationToken);

    public Task<Guid> UpsertJobStepAsync(Guid? jobStepId, UpsertJobStepRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertJobStepAsync(jobStepId, request, cancellationToken);

    public Task<Guid> UpsertJobScheduleAsync(Guid? jobScheduleId, UpsertJobScheduleRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertJobScheduleAsync(jobScheduleId, request, cancellationToken);

    public Task SetJobScheduleEnabledAsync(Guid jobScheduleId, SetJobScheduleEnabledRequest request, CancellationToken cancellationToken = default)
        => _repository.SetJobScheduleEnabledAsync(jobScheduleId, request, cancellationToken);

    public Task<Guid> TriggerJobRunAsync(Guid jobDefinitionId, TriggerJobRunRequest request, CancellationToken cancellationToken = default)
        => _repository.TriggerJobRunAsync(jobDefinitionId, request, cancellationToken);
}
