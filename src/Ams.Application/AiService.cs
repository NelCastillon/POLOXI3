using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class AiService : IAiService
{
    private readonly IAiRepository _repository;
    public AiService(IAiRepository repository) => _repository = repository;

    public Task<PagedResult<AiInsightDto>> GetInsightsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetInsightsAsync(tenantId, pageNumber, pageSize, cancellationToken);

    public Task<AiAccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _repository.GetAccountSummaryAsync(accountId, cancellationToken);

    public Task<PagedResult<AiNextActionDto>> GetNextActionsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetNextActionsAsync(tenantId, pageNumber, pageSize, cancellationToken);
}
