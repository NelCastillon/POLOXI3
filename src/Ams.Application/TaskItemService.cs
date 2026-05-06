using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class TaskItemService : ITaskItemService
{
    private readonly ITaskItemRepository _repository;

    public TaskItemService(ITaskItemRepository repository) => _repository = repository;

    public Task<TaskItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<TaskItemDto>> SearchAsync(Guid tenantId, string? searchTerm, string? stageCode, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, stageCode, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateTaskItemRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateTaskItemRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
}
