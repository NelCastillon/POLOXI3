using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IGLAccountRepository
{
    Task<GLAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<GLAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateGLAccountRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateGLAccountRequest request, CancellationToken cancellationToken = default);
}
