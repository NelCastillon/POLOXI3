using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface ICsrWorkbenchRepository
{
    Task<CsrWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
}
