using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Quotes;

namespace Ams.Application.Abstractions.Services;

public interface IQuoteService
{
    Task<Guid> CreateAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<QuoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<QuoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteLineDto>> GetLinesByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
}
