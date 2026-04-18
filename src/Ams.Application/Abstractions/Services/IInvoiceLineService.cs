using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IInvoiceLineService
{
    Task<InvoiceLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<InvoiceLineDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
