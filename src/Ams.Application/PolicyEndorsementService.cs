using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Application;

public sealed class PolicyEndorsementService : IPolicyEndorsementService
{
    private readonly IPolicyEndorsementRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public PolicyEndorsementService(IPolicyEndorsementRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetOptionsAsync(tenantId, cancellationToken);

    public Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(tenantId, endorsementId, cancellationToken);

    public Task<PolicyEndorsementWorkflowDetailDto?> GetWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _repository.GetWorkflowDetailAsync(tenantId, endorsementId, cancellationToken);

    public Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);

    public async Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a post-policy Endorsement must stay tenant-safe. When a parent
        // Account is supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "endorsement", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task<Guid> CreateTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(request.TenantId, request.PolicyId, request.CreatedByUserId, request.Changes);
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate);
        return _repository.CreateTransactionAsync(request, cancellationToken);
    }

    public Task SaveDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(request.TenantId, endorsementId, request.ModifiedByUserId, request.Changes);
        if (request.RowVersion.Length == 0) throw new ArgumentException("RowVersion is required.", nameof(request));
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate);
        return _repository.SaveDraftAsync(endorsementId, request, cancellationToken);
    }

    public async Task TransitionAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetWorkflowDetailAsync(request.TenantId, endorsementId, cancellationToken)
            ?? throw new KeyNotFoundException("The endorsement was not found in the tenant.");
        var transition = detail.AvailableTransitions.SingleOrDefault(x => string.Equals(x.ToStatusCode, request.ToStatusCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested endorsement status transition is not allowed.");
        EnsurePermission(request.GrantedPermissions, transition.RequiredPermissionCode);
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        await _repository.TransitionAsync(endorsementId, request, cancellationToken);
    }

    public Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(request.GrantedPermissions, "ENDORSEMENT_APPROVE");
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        return _repository.DecideApprovalAsync(endorsementId, approvalId, request, cancellationToken);
    }

    public async Task<Guid> ReverseAsync(Guid endorsementId, ReversePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(request.GrantedPermissions, "ENDORSEMENT_REVERSE");
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate);
        var detail = await _repository.GetWorkflowDetailAsync(request.TenantId, endorsementId, cancellationToken)
            ?? throw new KeyNotFoundException("The endorsement was not found in the tenant.");
        if (!string.Equals(detail.Endorsement.Status, "Completed", StringComparison.OrdinalIgnoreCase) || detail.Endorsement.ReversedByEndorsementId.HasValue)
            throw new InvalidOperationException("Only an unreversed completed endorsement can be reversed.");
        if (!detail.Endorsement.PolicyId.HasValue || !request.ActorUserId.HasValue)
            throw new InvalidOperationException("The endorsement must be linked to a policy and authenticated user.");

        var create = new CreatePolicyEndorsementTransactionRequest
        {
            TenantId = request.TenantId,
            PolicyId = detail.Endorsement.PolicyId.Value,
            EndorsementTypeCode = detail.Endorsement.EndorsementType,
            ReasonCode = "Other",
            EffectiveDate = request.EffectiveDate,
            Description = $"Reversal of {detail.Endorsement.EndorsementNumber}: {request.Reason}",
            PriorityCode = detail.Endorsement.Priority,
            CarrierMethodCode = detail.Endorsement.CarrierMethodCode,
            InternalNotes = request.Reason,
            FinancialImpact = new PolicyEndorsementFinancialImpactInput
            {
                CurrencyCode = detail.Endorsement.CurrencyCode,
                PremiumChange = -detail.Endorsement.PremiumDelta,
                AgencyFee = -detail.Endorsement.AgencyFeeDelta,
                Taxes = -detail.Endorsement.TaxDelta,
                ProratedPremiumChange = -detail.Endorsement.ProratedPremiumDelta,
                BillingImpactCode = detail.Endorsement.PremiumDelta + detail.Endorsement.AgencyFeeDelta + detail.Endorsement.TaxDelta > 0 ? "RefundCredit" : "InvoiceImmediately",
                CommissionImpactCode = detail.Endorsement.CommissionImpactCode
            },
            Changes = detail.Changes.Select(ReverseChange).ToList(),
            CreatedByUserId = request.ActorUserId,
            AllowBackdate = request.AllowBackdate,
            ReversalOfEndorsementId = endorsementId
        };
        ValidateTransaction(create.TenantId, create.PolicyId, create.CreatedByUserId, create.Changes);
        return await _repository.CreateTransactionAsync(create, cancellationToken);
    }

    public Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(endorsementId, request, cancellationToken);

    public Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStatusAsync(endorsementId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertDeltaAsync(request, cancellationToken);

    public Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.ArchiveAsync(endorsementId, modifiedByUserId, cancellationToken);

    private static void ValidateTransaction(Guid tenantId, Guid parentId, Guid? userId, IReadOnlyList<PolicyEndorsementChangeInput> changes)
    {
        if (tenantId == Guid.Empty || parentId == Guid.Empty) throw new ArgumentException("Tenant and policy or endorsement identifiers are required.");
        if (!userId.HasValue || userId == Guid.Empty) throw new ArgumentException("An authenticated user is required.");
        if (changes.Count == 0) throw new ArgumentException("At least one typed policy change is required.");
        foreach (var change in changes)
        {
            var results = change.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(change)).ToArray();
            if (results.Length > 0) throw new ArgumentException(results[0].ErrorMessage);
        }
    }

    private static void EnsureEffectiveDate(DateTime effectiveDate, bool allowBackdate)
    {
        if (effectiveDate.Date < DateTime.UtcNow.Date && !allowBackdate)
            throw new UnauthorizedAccessException("Backdating an endorsement requires ENDORSEMENT_BACKDATE permission.");
    }

    private static void EnsurePermission(IReadOnlyCollection<string> permissions, string? requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission)) return;
        if (!permissions.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase) &&
            !permissions.Contains("ENDORSEMENT_MANAGE", StringComparer.OrdinalIgnoreCase) &&
            !permissions.Contains("NAV_ALL", StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Permission {requiredPermission} is required.");
    }

    private static PolicyEndorsementChangeInput ReverseChange(PolicyEndorsementChangeDto change) => new()
    {
        CategoryCode = change.CategoryCode,
        OperationCode = change.OperationCode switch { "Add" => "Remove", "Remove" => "Add", _ => change.OperationCode },
        EntityKey = change.EntityKey,
        Summary = $"Reverse: {change.Summary}",
        Insured = ReverseTyped(change.Insured),
        Vehicle = ReverseTyped(change.Vehicle),
        Driver = ReverseTyped(change.Driver),
        Coverage = ReverseTyped(change.Coverage),
        Property = ReverseTyped(change.Property),
        Commercial = ReverseTyped(change.Commercial),
        Financial = ReverseTyped(change.Financial),
        Legal = ReverseTyped(change.Legal)
    };

    private static T? ReverseTyped<T>(T? source) where T : class, new()
    {
        if (source is null) return null;
        var result = new T();
        var properties = typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite).ToDictionary(property => property.Name);
        foreach (var property in properties.Values)
        {
            if (property.Name == "ChangeId") continue;
            if (property.Name.StartsWith("Before", StringComparison.Ordinal) && properties.TryGetValue($"After{property.Name[6..]}", out var after))
                property.SetValue(result, after.GetValue(source));
            else if (property.Name.StartsWith("After", StringComparison.Ordinal) && properties.TryGetValue($"Before{property.Name[5..]}", out var before))
                property.SetValue(result, before.GetValue(source));
            else
                property.SetValue(result, property.GetValue(source));
        }
        return result;
    }
}
