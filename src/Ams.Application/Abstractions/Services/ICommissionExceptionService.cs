using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionExceptionService
{
    Task<CommissionExceptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionExceptionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? severityCode = null, string? typeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionExceptionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionExceptionRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
