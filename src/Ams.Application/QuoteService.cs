using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Quotes;

namespace Ams.Application;

public sealed class QuoteService : IQuoteService
{
    private readonly IQuoteRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IOpportunityRepository _opportunityRepository;

    public QuoteService(
        IQuoteRepository repository,
        IAccountRepository accountRepository,
        IOpportunityRepository opportunityRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _opportunityRepository = opportunityRepository;
    }

    public async Task<Guid> CreateAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateChainAsync(request, cancellationToken);
        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task<QuoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<QuoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task UpdateAsync(UpdateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateChainAsync(request, cancellationToken);
        await _repository.UpdateAsync(request, cancellationToken);
    }

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<QuoteLineDto>> GetLinesByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _repository.GetLinesByQuoteIdAsync(quoteId, cancellationToken);

    private async Task ValidateChainAsync(CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        // Enterprise rule: a Quote must trace back to a real Account (and Opportunity when
        // supplied) within the same tenant so it is never orphaned or cross-tenant.
        await TenantGuard.EnsureParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "quote", cancellationToken);

        var opportunity = await TenantGuard.EnsureOptionalParentAsync(request.OpportunityId, request.TenantId, _opportunityRepository.GetByIdAsync, o => o.TenantId, "Parent opportunity", "quote", cancellationToken);
        if (opportunity is not null && opportunity.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("Parent opportunity is not linked to the supplied account; the quote chain is inconsistent.");
        }
    }
}
