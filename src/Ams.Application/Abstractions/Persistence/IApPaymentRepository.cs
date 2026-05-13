using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IApPaymentRepository
{
    Task<ApPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ApPaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateApPaymentRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateApPaymentRequest request, CancellationToken cancellationToken = default);
}
