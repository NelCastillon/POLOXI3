using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface ISecurityEventLogRepository
{
    Task<PagedResult<SecurityEventLogDto>> SearchAsync(string? searchTerm, string? eventTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
