using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application;

public sealed class AgreementService : IAgreementService
{
    private readonly IAgreementRepository _repository;

    public AgreementService(IAgreementRepository repository)
    {
        _repository = repository;
    }

    public Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AgreementDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateAgreementRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);
}
