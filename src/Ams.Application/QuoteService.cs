using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Quotes;

namespace Ams.Application;

public sealed class QuoteService : IQuoteService
{
    private readonly IQuoteRepository _repository;

    public QuoteService(IQuoteRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<QuoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<QuoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(UpdateQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<QuoteLineDto>> GetLinesByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _repository.GetLinesByQuoteIdAsync(quoteId, cancellationToken);
}
