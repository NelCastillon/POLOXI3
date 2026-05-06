using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IAccountingWorkbenchService
{
    Task<AccountingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
}
