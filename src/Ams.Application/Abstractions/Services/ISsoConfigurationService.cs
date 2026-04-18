using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ISsoConfigurationService
{
    Task<SsoConfigurationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<SsoConfigurationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
