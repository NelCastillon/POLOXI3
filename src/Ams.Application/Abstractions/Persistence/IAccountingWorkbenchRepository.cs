using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccountingWorkbenchRepository
{
    Task<AccountingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default);
}
