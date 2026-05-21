using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionService
{
    Task<CommissionPayeeDto?> GetPayeeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPayeeDto>> SearchPayeesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreatePayeeAsync(CreateCommissionPayeeRequest request, CancellationToken cancellationToken = default);
    Task UpdatePayeeAsync(Guid id, UpdateCommissionPayeeRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default);
    Task<CommissionTransactionDto?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionTransactionDto>> SearchTransactionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateTransactionAsync(CreateCommissionTransactionRequest request, CancellationToken cancellationToken = default);
    Task UpdateTransactionAsync(Guid id, UpdateCommissionTransactionRequest request, CancellationToken cancellationToken = default);
    Task<CommissionPayoutDto?> GetPayoutByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPayoutDto>> SearchPayoutsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreatePayoutAsync(CreateCommissionPayoutRequest request, CancellationToken cancellationToken = default);
    Task UpdatePayoutAsync(Guid id, UpdateCommissionPayoutRequest request, CancellationToken cancellationToken = default);
}
