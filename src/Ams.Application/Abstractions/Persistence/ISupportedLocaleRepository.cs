using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface ISupportedLocaleRepository
{
    Task<SupportedLocaleDto?> GetByIdAsync(Guid localeId, CancellationToken cancellationToken = default);
    Task<SupportedLocaleDto?> GetByCodeAsync(string localeCode, CancellationToken cancellationToken = default);
    Task<PagedResult<SupportedLocaleDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
