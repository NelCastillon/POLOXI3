using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionService
{
    Task<CommissionPayeeDto?> GetPayeeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPayeeDto>> SearchPayeesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<CommissionTransactionDto?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionTransactionDto>> SearchTransactionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<CommissionPayoutDto?> GetPayoutByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPayoutDto>> SearchPayoutsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
