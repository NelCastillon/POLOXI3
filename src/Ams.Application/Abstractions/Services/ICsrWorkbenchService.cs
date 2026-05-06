using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface ICsrWorkbenchService
{
    Task<CsrWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
}
