using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaViolations;

namespace Ams.Application;

public sealed class QuotaViolationService : IQuotaViolationService
{
    private readonly IQuotaViolationRepository _repository;

    public QuotaViolationService(IQuotaViolationRepository repository) => _repository = repository;

    public Task<PagedResult<QuotaViolationDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, severityCode, pageNumber, pageSize, cancellationToken);

    public Task<QuotaViolationDto?> GetByIdAsync(Guid violationId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(violationId, cancellationToken);

    public Task AcknowledgeAsync(Guid violationId, AcknowledgeQuotaViolationRequest request, CancellationToken cancellationToken = default)
        => _repository.AcknowledgeAsync(violationId, request, cancellationToken);

    public Task ResolveAsync(Guid violationId, ResolveQuotaViolationRequest request, CancellationToken cancellationToken = default)
        => _repository.ResolveAsync(violationId, request, cancellationToken);

    public Task NotifyAsync(Guid violationId, NotifyQuotaViolationRequest request, CancellationToken cancellationToken = default)
        => _repository.NotifyAsync(violationId, request, cancellationToken);

    public Task ApplyRestrictionAsync(Guid violationId, ApplyRestrictionRequest request, CancellationToken cancellationToken = default)
        => _repository.ApplyRestrictionAsync(violationId, request, cancellationToken);

    public Task GrantTemporaryIncreaseAsync(Guid violationId, GrantTemporaryIncreaseRequest request, CancellationToken cancellationToken = default)
        => _repository.GrantTemporaryIncreaseAsync(violationId, request, cancellationToken);

    public Task ConvertToOverageAsync(Guid violationId, ConvertToOverageRequest request, CancellationToken cancellationToken = default)
        => _repository.ConvertToOverageAsync(violationId, request, cancellationToken);

    public Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
        => _repository.GetOpenCountAsync(cancellationToken);
}
