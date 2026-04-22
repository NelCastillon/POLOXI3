using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Lobs;

namespace Ams.Application.Abstractions.Services;

public interface ILineOfBusinessService
{
    Task<LineOfBusinessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<LineOfBusinessDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateLineOfBusinessRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateLineOfBusinessRequest request, CancellationToken cancellationToken = default);
}
