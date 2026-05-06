using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface ITaskItemService
{
    Task<TaskItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TaskItemDto>> SearchAsync(Guid tenantId, string? searchTerm, string? stageCode, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTaskItemRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTaskItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
