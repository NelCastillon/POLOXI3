using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface ITaskTypeService
{
    Task<TaskTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TaskTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTaskTypeRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTaskTypeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
