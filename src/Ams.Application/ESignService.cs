using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class ESignService : IESignService
{
    private readonly IESignRepository _repository;
    public ESignService(IESignRepository repository) => _repository = repository;

    public Task<IReadOnlyList<ESignRequestDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task<ESignRequestDto?> GetByIdAsync(Guid tenantId, Guid eSignRequestId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(tenantId, eSignRequestId, cancellationToken);

    public Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.DocumentId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Tenant, document, and idempotency key are required.");
        if (string.IsNullOrWhiteSpace(request.SignerName) || string.IsNullOrWhiteSpace(request.SignerEmail))
            throw new ArgumentException("Signer name and email are required.");
        return _repository.SendAsync(request with { SignerName = request.SignerName.Trim(), SignerEmail = request.SignerEmail.Trim(), IdempotencyKey = request.IdempotencyKey.Trim() }, cancellationToken);
    }

    public Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default)
        => _repository.VoidAsync(request, cancellationToken);

    public Task RemindAsync(Guid tenantId, Guid eSignRequestId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.RemindAsync(tenantId, eSignRequestId, modifiedByUserId, cancellationToken);

    public Task ProcessDocuSignCallbackAsync(ProcessDocuSignCallbackRequest request, CancellationToken cancellationToken = default)
        => _repository.ProcessDocuSignCallbackAsync(request, cancellationToken);
}
