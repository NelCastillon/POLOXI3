using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;

namespace Ams.Application.Abstractions.Persistence;

public interface ISubmissionRepository
{
    // Submission register
    Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default);

    // Markets
    Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default);
    Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default);
    Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default);

    // Quotes
    Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default);

    // Proposals
    Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default);

    // Appetite
    Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default);

    // Bind
    Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default);
    Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default);
    Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default);
}

public interface ISubmissionReferenceOptionRepository
{
    Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default);
}
