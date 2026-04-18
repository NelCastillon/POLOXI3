using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IPrebillBatchRepository
{
    Task<PrebillBatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PrebillBatchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
