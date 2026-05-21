using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;

namespace Ams.Application;

public sealed class AcordFormService : IAcordFormService
{
    private readonly IAcordFormRepository _repository;

    public AcordFormService(IAcordFormRepository repository) => _repository = repository;

    public Task<IReadOnlyList<AcordFormDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantAsync(tenantId, cancellationToken);

    public Task<AcordFormDto?> GetByIdAsync(Guid acordFormId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(acordFormId, cancellationToken);

    public Task<Guid> CreateAsync(CreateAcordFormRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateStatusAsync(UpdateAcordFormStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(request, cancellationToken);

    public Task PrefillAsync(PrefillAcordFormRequest request, CancellationToken cancellationToken = default)
        => _repository.PrefillAsync(request, cancellationToken);
}
