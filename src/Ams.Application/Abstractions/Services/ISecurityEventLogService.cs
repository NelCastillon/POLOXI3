using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ISecurityEventLogService
{
    Task<PagedResult<SecurityEventLogDto>> SearchAsync(Guid? tenantId = null, string? searchTerm = null, string? eventTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
