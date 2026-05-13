using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Services;

public interface IApInvoiceService
{
    Task<ApInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ApInvoiceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateApInvoiceRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateApInvoiceRequest request, CancellationToken cancellationToken = default);
}
