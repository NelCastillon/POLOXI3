using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application;

public sealed class UserAuditTrailService : IUserAuditTrailService
{
    private readonly IUserAuditTrailRepository _repository;

    public UserAuditTrailService(IUserAuditTrailRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> LogAsync(LogUserAuditTrailRequest request, CancellationToken cancellationToken = default)
        => _repository.LogAsync(request, cancellationToken);

    public Task<PagedResult<UserAuditTrailDto>> SearchAsync(SearchUserAuditTrailRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(request, cancellationToken);

    public Task<UserAuditTrailDto?> GetByIdAsync(Guid tenantId, Guid auditTrailId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(tenantId, auditTrailId, cancellationToken);

    public Task<UserAuditTrailSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, CancellationToken cancellationToken = default)
        => _repository.GetSummaryAsync(tenantId, fromDateUtc, toDateUtc, cancellationToken);

    public Task<IReadOnlyList<UserAuditActionTypeDto>> GetActionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetActionTypesAsync(tenantId, cancellationToken);
}
