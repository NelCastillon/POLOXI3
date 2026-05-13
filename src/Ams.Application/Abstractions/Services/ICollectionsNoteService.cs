using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Services;

public interface ICollectionsNoteService
{
    Task<CollectionsNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CollectionsNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCollectionsNoteRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCollectionsNoteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
