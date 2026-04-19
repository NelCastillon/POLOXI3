using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ISystemLogService
{
    Task<PagedResult<SystemLogDto>> SearchAsync(string? keyword = null, string? level = null, string? serviceName = null, string? regionCode = null, string? correlationId = null, string? tenantId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<SystemLogDto?> GetByIdAsync(Guid systemLogId, CancellationToken cancellationToken = default);
}
