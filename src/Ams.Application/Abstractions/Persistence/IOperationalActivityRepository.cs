using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Persistence;

public interface IOperationalActivityRepository
{
    Task<OperationalActivityLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OperationalActivityLogDto>> SearchAsync(Guid tenantId, Guid? accountId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default);
}
