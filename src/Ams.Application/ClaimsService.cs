using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Claims;

namespace Ams.Application;

public sealed class ClaimsService : IClaimsService
{
    private readonly IClaimsRepository _repository;

    public ClaimsService(IClaimsRepository repository) => _repository = repository;

    public Task<PagedResult<ClaimDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lob, string? catCode, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, status, lob, catCode, pageNumber, pageSize, cancellationToken);

    public Task<ClaimDetailDto?> GetDetailAsync(Guid tenantId, Guid claimId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(tenantId, claimId, cancellationToken);

    public Task<IReadOnlyList<ClaimOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetOptionsAsync(tenantId, cancellationToken);

    public Task<Guid> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Claim must always be tenant-scoped and tied to a real policy
        // and claimant. It must never be created without a tenant context or parent policy.
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A Claim requires a tenant context. TenantId was not supplied.");
        }

        if (string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            throw new InvalidOperationException("A Claim requires a parent Policy. PolicyNumber was not supplied.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            throw new InvalidOperationException("A Claim requires an Account context. AccountName was not supplied.");
        }

        return _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(claimId, request, cancellationToken);

    public Task UpdateFollowUpAsync(Guid claimId, UpdateClaimFollowUpRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateFollowUpAsync(claimId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(CreateClaimActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task<PagedResult<CatEventDto>> SearchCatEventsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.SearchCatEventsAsync(tenantId, cancellationToken);

    public Task<Guid> CreateCatEventAsync(CreateCatEventRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateCatEventAsync(request, cancellationToken);

    public Task<CatastrophePageDto> GetCatastrophePageAsync(Guid tenantId, Guid? catEventId, CancellationToken cancellationToken = default)
        => _repository.GetCatastrophePageAsync(tenantId, catEventId, cancellationToken);

    public Task MarkAffectedInsuredContactedAsync(Guid affectedInsuredId, CancellationToken cancellationToken = default)
        => _repository.MarkAffectedInsuredContactedAsync(affectedInsuredId, cancellationToken);

    public Task<int> ApplyGeoTagAsync(Guid catEventId, string? states, string? counties, string? zips, string? lob, decimal? minTiv, CancellationToken cancellationToken = default)
        => _repository.ApplyGeoTagAsync(catEventId, states, counties, zips, lob, minTiv, cancellationToken);

    public Task<int> SendCatBlastAsync(CatBlastRequest request, CancellationToken cancellationToken = default)
        => _repository.SendCatBlastAsync(request, cancellationToken);

    public Task<Guid> CreateFastCatFnolAsync(FastCatFnolRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateFastCatFnolAsync(request, cancellationToken);

    public Task<Guid> AssignAdjusterAsync(AssignClaimAdjusterRequest request, CancellationToken cancellationToken = default) => _repository.AssignAdjusterAsync(request, cancellationToken);
    public Task<Guid> UpsertPartyAsync(UpsertClaimPartyRequest request, CancellationToken cancellationToken = default) => _repository.UpsertPartyAsync(request, cancellationToken);
    public Task<Guid> CreateFinancialTransactionAsync(CreateClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (!ClaimRules.IsFinancialTransactionType(request.TransactionTypeCode)) throw new InvalidOperationException("Unsupported claim financial transaction type.");
        return _repository.CreateFinancialTransactionAsync(request, cancellationToken);
    }
    public Task<Guid> ReverseFinancialTransactionAsync(ReverseClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default) => _repository.ReverseFinancialTransactionAsync(request, cancellationToken);
    public Task<Guid> CreateNoteAsync(CreateClaimNoteRequest request, CancellationToken cancellationToken = default) => _repository.CreateNoteAsync(request, cancellationToken);
    public Task<Guid> CreateTaskAsync(CreateClaimTaskRequest request, CancellationToken cancellationToken = default) => _repository.CreateTaskAsync(request, cancellationToken);
    public Task CompleteTaskAsync(CompleteClaimTaskRequest request, CancellationToken cancellationToken = default) => _repository.CompleteTaskAsync(request, cancellationToken);
    public Task<Guid> LinkDocumentAsync(LinkClaimDocumentRequest request, CancellationToken cancellationToken = default) => _repository.LinkDocumentAsync(request, cancellationToken);
    public Task<LossRunImportResultDto> ImportLossRunAsync(ImportLossRunRequest request, CancellationToken cancellationToken = default) => _repository.ImportLossRunAsync(request, cancellationToken);
    public Task<IReadOnlyList<LossRunDto>> GetLossRunsAsync(Guid tenantId, Guid? accountId, CancellationToken cancellationToken = default) => _repository.GetLossRunsAsync(tenantId, accountId, cancellationToken);
}
