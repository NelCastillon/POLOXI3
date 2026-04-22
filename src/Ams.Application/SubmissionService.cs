using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;

namespace Ams.Application;

public sealed class SubmissionService : ISubmissionService
{
    private readonly ISubmissionRepository _repository;
    public SubmissionService(ISubmissionRepository repository) => _repository = repository;

    public Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken);

    public Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.AssignAsync(id, request, cancellationToken);

    public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetMarketsAsync(submissionId, cancellationToken);

    public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetMarketSuggestionsAsync(submissionId, cancellationToken);

    public Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
        => _repository.AddMarketAsync(request, cancellationToken);

    public Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateMarketStatusAsync(submissionMarketId, request, cancellationToken);

    public Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
        => _repository.RemoveMarketAsync(submissionMarketId, cancellationToken);

    public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteComparisonAsync(submissionId, cancellationToken);

    public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteByIdAsync(quoteId, cancellationToken);

    public Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => _repository.GetProposalByIdAsync(proposalId, cancellationToken);

    public Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
        => _repository.GenerateProposalAsync(request, cancellationToken);

    public Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAppetiteAsync(request, cancellationToken);

    public Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBySubmissionAsync(submissionId, cancellationToken);

    public Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
        => _repository.BindPolicyAsync(request, cancellationToken);
}
