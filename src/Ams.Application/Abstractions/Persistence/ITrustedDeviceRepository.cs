using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;

namespace Ams.Application.Abstractions.Persistence;

public interface ITrustedDeviceRepository
{
    Task<TrustedDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TrustedDeviceDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, bool? isActive, bool? highRiskOnly, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task RevokeAsync(RevokeTrustedDeviceRequest request, CancellationToken cancellationToken = default);
    Task SubmitRiskReviewAsync(RiskReviewRequest request, CancellationToken cancellationToken = default);
}
