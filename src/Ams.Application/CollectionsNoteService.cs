using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application;

public sealed class CollectionsNoteService : ICollectionsNoteService
{
    private readonly ICollectionsNoteRepository _repository;
    public CollectionsNoteService(ICollectionsNoteRepository repository) => _repository = repository;
    public Task<CollectionsNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<CollectionsNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateCollectionsNoteRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateCollectionsNoteRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
}
