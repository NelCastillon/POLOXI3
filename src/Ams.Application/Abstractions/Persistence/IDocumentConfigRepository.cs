using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentConfigRepository
{
    Task<DocumentConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<DocumentConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateDocumentConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateDocumentConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
