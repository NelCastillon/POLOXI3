using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IPlatformEventRepository
{
    Task<PagedResult<PlatformEventDto>> SearchAsync(string? searchTerm = null, string? eventTypeCode = null, string? processingStatus = null, string? sourceService = null, Guid? tenantId = null, string? correlationId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PlatformEventDto?> GetByIdAsync(Guid platformEventId, CancellationToken cancellationToken = default);
    Task ReplayAsync(Guid platformEventId, CancellationToken cancellationToken = default);
}
