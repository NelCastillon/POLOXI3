using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class CsrWorkbenchService : ICsrWorkbenchService
{
    private readonly ICsrWorkbenchRepository _repository;

    public CsrWorkbenchService(ICsrWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<CsrWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, teamScope, branchId, teamId, cancellationToken);
}
