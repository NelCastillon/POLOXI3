using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class TaskTypeService : ITaskTypeService
{
    private readonly ITaskTypeRepository _repository;

    public TaskTypeService(ITaskTypeRepository repository) => _repository = repository;

    public Task<TaskTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<TaskTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateTaskTypeRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateTaskTypeRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
}
