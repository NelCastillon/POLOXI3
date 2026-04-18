using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class CommissionService : ICommissionService
{
    private readonly ICommissionPayeeRepository _payeeRepo;
    private readonly ICommissionTransactionRepository _txRepo;
    private readonly ICommissionPayoutRepository _payoutRepo;

    public CommissionService(ICommissionPayeeRepository payeeRepo, ICommissionTransactionRepository txRepo, ICommissionPayoutRepository payoutRepo)
    {
        _payeeRepo = payeeRepo;
        _txRepo = txRepo;
        _payoutRepo = payoutRepo;
    }

    public Task<CommissionPayeeDto?> GetPayeeByIdAsync(Guid id, CancellationToken cancellationToken = default) => _payeeRepo.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<CommissionPayeeDto>> SearchPayeesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _payeeRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<CommissionTransactionDto?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default) => _txRepo.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<CommissionTransactionDto>> SearchTransactionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _txRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<CommissionPayoutDto?> GetPayoutByIdAsync(Guid id, CancellationToken cancellationToken = default) => _payoutRepo.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<CommissionPayoutDto>> SearchPayoutsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _payoutRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
