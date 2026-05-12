using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Quotes;

namespace Ams.Application.Abstractions.Persistence;

public interface IQuoteRepository
{
    Task<Guid> CreateAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<QuoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<QuoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateQuoteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteLineDto>> GetLinesByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
}
