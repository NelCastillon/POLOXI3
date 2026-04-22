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

    public Task<ESignRequestDto?> GetByIdAsync(Guid eSignRequestId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(eSignRequestId, cancellationToken);

    public Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default)
        => _repository.SendAsync(request, cancellationToken);

    public Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default)
        => _repository.VoidAsync(request, cancellationToken);

    public Task RemindAsync(Guid eSignRequestId, CancellationToken cancellationToken = default)
        => _repository.RemindAsync(eSignRequestId, cancellationToken);
}
