using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyCertificates;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyCertificateService
{
    Task<PagedResult<PolicyCertificateDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? certificateType, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<PolicyCertificateDto?> GetByIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default);
    Task<PolicyCertificateDto?> GetByNumberAsync(Guid tenantId, string certificateNumber, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyCertificateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid certificateId, UpdatePolicyCertificateRequest request, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid certificateId, RevokePolicyCertificateRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid certificateId, RestorePolicyCertificateRequest request, CancellationToken cancellationToken = default);
    Task MarkDeliveredAsync(Guid certificateId, PolicyCertificateActionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid certificateId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
