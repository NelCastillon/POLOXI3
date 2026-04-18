using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface IOperationalActivityService
{
    Task<OperationalActivityLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OperationalActivityLogDto>> SearchAsync(Guid tenantId, Guid? accountId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default);
}
