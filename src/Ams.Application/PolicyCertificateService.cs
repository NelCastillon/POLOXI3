using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyCertificates;

namespace Ams.Application;

public sealed class PolicyCertificateService : IPolicyCertificateService
{
    private readonly IPolicyCertificateRepository _repository;

    public PolicyCertificateService(IPolicyCertificateRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<PolicyCertificateDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? certificateType, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, status, certificateType, pageNumber, pageSize, cancellationToken);

    public Task<PolicyCertificateDto?> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(certificateId, cancellationToken);

    public Task<PolicyCertificateDto?> GetByNumberAsync(Guid tenantId, string certificateNumber, CancellationToken cancellationToken = default)
        => _repository.GetByNumberAsync(tenantId, certificateNumber, cancellationToken);

    public Task<Guid> CreateAsync(CreatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid certificateId, UpdatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(certificateId, request, cancellationToken);

    public Task RevokeAsync(Guid certificateId, RevokePolicyCertificateRequest request, CancellationToken cancellationToken = default)
        => _repository.RevokeAsync(certificateId, request, cancellationToken);

    public Task RestoreAsync(Guid certificateId, RestorePolicyCertificateRequest request, CancellationToken cancellationToken = default)
        => _repository.RestoreAsync(certificateId, request, cancellationToken);

    public Task MarkDeliveredAsync(Guid certificateId, PolicyCertificateActionRequest request, CancellationToken cancellationToken = default)
        => _repository.MarkDeliveredAsync(certificateId, request, cancellationToken);

    public Task DeleteAsync(Guid certificateId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(certificateId, tenantId, modifiedByUserId, cancellationToken);
}
