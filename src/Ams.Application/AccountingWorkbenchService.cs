using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class AccountingWorkbenchService : IAccountingWorkbenchService
{
    private readonly IAccountingWorkbenchRepository _repository;

    public AccountingWorkbenchService(IAccountingWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<AccountingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
        => _repository.GetWorkbenchAsync(tenantId, userId, teamScope, branchId, teamId, cancellationToken);
}
