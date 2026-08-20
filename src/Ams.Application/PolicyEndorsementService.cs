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

    public Task<PolicyEndorsementCatalogDto> GetCatalogAsync(Guid tenantId, string? lineOfBusinessCode = null, CancellationToken cancellationToken = default)
        => _repository.GetCatalogAsync(tenantId, lineOfBusinessCode, cancellationToken);

    public Task<PolicyEndorsementTypeCatalogDto?> GetTypeCatalogAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken = default)
        => _repository.GetTypeCatalogAsync(tenantId, typeCode, cancellationToken);

    public Task UpdateTypeProfileAsync(Guid endorsementTypeId, UpdatePolicyEndorsementTypeProfileRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateTypeProfileAsync(endorsementTypeId, request, cancellationToken);

    public Task ReplaceTypeConfigurationAsync(Guid endorsementTypeId, ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken = default)
        => _repository.ReplaceTypeConfigurationAsync(endorsementTypeId, request, cancellationToken);

    public Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(tenantId, endorsementId, cancellationToken);

    public Task<PolicyEndorsementWorkflowDetailDto?> GetWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        => _repository.GetWorkflowDetailAsync(tenantId, endorsementId, cancellationToken);

    public Task<PolicyEndorsementRoutePreviewDto?> GetRoutePreviewAsync(Guid tenantId, Guid endorsementId, string routePurposeCode, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (routePurposeCode is not ("Approval" or "InformationRequest"))
            throw new ArgumentException("Route purpose must be Approval or InformationRequest.", nameof(routePurposeCode));
        return _repository.GetRoutePreviewAsync(tenantId, endorsementId, routePurposeCode, actorUserId, cancellationToken);
    }

    public Task<IReadOnlyList<PolicyEndorsementApprovalInboxItemDto>> GetApprovalInboxAsync(Guid tenantId, Guid assignedToUserId, CancellationToken cancellationToken = default)
        => _repository.GetApprovalInboxAsync(tenantId, assignedToUserId, cancellationToken);

    public Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyWorkspaceAsync(tenantId, policyId, cancellationToken);

    public async Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a post-policy Endorsement must stay tenant-safe. When a parent
        // Account is supplied it must exist and belong to the same tenant.
        await TenantGuard.EnsureOptionalParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Parent account", "endorsement", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public async Task<Guid> CreateTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(request.TenantId, request.PolicyId, request.CreatedByUserId, request.Changes);
        var type = await RequireTypeAsync(request.TenantId, request.EndorsementTypeCode, cancellationToken);
        ApplyProfileDefaults(type, request);
        ValidateTypeConfiguration(type, request.Changes, request.PriorityCode, request.CarrierMethodCode, request.FinancialImpact);
        await ValidateOptionsAsync(request.TenantId, request.ReasonCode, request.PriorityCode, request.FinancialImpact, cancellationToken);
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate && type.Profile?.SupportsBackdate == true);
        return await _repository.CreateTransactionAsync(request, cancellationToken);
    }

    public async Task SaveDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(request.TenantId, endorsementId, request.ModifiedByUserId, request.Changes);
        if (request.RowVersion.Length == 0) throw new ArgumentException("RowVersion is required.", nameof(request));
        var type = await RequireTypeAsync(request.TenantId, request.EndorsementTypeCode, cancellationToken);
        ApplyProfileDefaults(type, request);
        ValidateTypeConfiguration(type, request.Changes, request.PriorityCode, request.CarrierMethodCode, request.FinancialImpact);
        await ValidateOptionsAsync(request.TenantId, request.ReasonCode, request.PriorityCode, request.FinancialImpact, cancellationToken);
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate && type.Profile?.SupportsBackdate == true);
        await _repository.SaveDraftAsync(endorsementId, request, cancellationToken);
    }

    public async Task TransitionAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RowVersion.Length == 0) throw new ArgumentException("RowVersion is required.", nameof(request));
        var detail = await _repository.GetWorkflowDetailAsync(request.TenantId, endorsementId, cancellationToken)
            ?? throw new KeyNotFoundException("The endorsement was not found in the tenant.");
        var transition = detail.AvailableTransitions.SingleOrDefault(x => string.Equals(x.ToStatusCode, request.ToStatusCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested endorsement status transition is not allowed.");
        var type = await RequireTypeAsync(request.TenantId, detail.Endorsement.EndorsementType, cancellationToken);
        var typeRule = type.WorkflowRules.SingleOrDefault(x => string.Equals(x.FromStatusCode, detail.Endorsement.Status, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ToStatusCode, request.ToStatusCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested transition is not configured for this endorsement type.");
        EnsurePermission(request.GrantedPermissions, transition.RequiredPermissionCode);
        EnsurePermission(request.GrantedPermissions, typeRule.RequiredPermissionCode);
        if (transition.RequiresNotes && string.IsNullOrWhiteSpace(request.Notes))
            throw new ArgumentException("Reviewer notes are required for the selected workflow action.", nameof(request));
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        await _repository.TransitionAsync(endorsementId, request, cancellationToken);
    }

    private async Task<PolicyEndorsementTypeCatalogDto> RequireTypeAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken)
        => await _repository.GetTypeCatalogAsync(tenantId, typeCode, cancellationToken)
            ?? throw new ArgumentException("The endorsement type is inactive or does not exist in the tenant.", nameof(typeCode));

    private static void ValidateTypeConfiguration(
        PolicyEndorsementTypeCatalogDto type,
        IReadOnlyCollection<PolicyEndorsementChangeInput> changes,
        string priorityCode,
        string? carrierMethodCode,
        PolicyEndorsementFinancialImpactInput financialImpact)
    {
        var profile = type.Profile ?? throw new InvalidOperationException("The endorsement type profile is not configured.");
        if (changes.Any(change => !string.Equals(change.CategoryCode, profile.CategoryCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"All changes for '{type.TypeName}' must use category '{profile.CategoryCode}'.", nameof(changes));
        if (changes.Any(change => !string.Equals(change.OperationCode, profile.DefaultOperationCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"All changes for '{type.TypeName}' must use operation '{profile.DefaultOperationCode}'.", nameof(changes));
        if (!string.IsNullOrWhiteSpace(carrierMethodCode) && type.CarrierMethods.Count > 0 && !type.CarrierMethods.Any(x => string.Equals(x.CarrierMethodCode, carrierMethodCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The carrier method is not configured for this endorsement type.", nameof(carrierMethodCode));
        if (!profile.IsPremiumBearing && (financialImpact.PremiumChange != 0 || financialImpact.AgencyFee != 0 || financialImpact.Taxes != 0))
            throw new ArgumentException("The selected endorsement type is configured as non-premium bearing.", nameof(financialImpact));
        if (string.IsNullOrWhiteSpace(priorityCode))
            throw new ArgumentException("Priority is required.", nameof(priorityCode));
    }

    private static void ApplyProfileDefaults(PolicyEndorsementTypeCatalogDto type, CreatePolicyEndorsementTransactionRequest request)
    {
        var profile = type.Profile!;
        request.CarrierMethodCode = string.IsNullOrWhiteSpace(request.CarrierMethodCode) ? profile.CarrierMethodCode : request.CarrierMethodCode;
        request.FinancialImpact.BillingImpactCode = string.IsNullOrWhiteSpace(request.FinancialImpact.BillingImpactCode) ? profile.BillingImpactCode : request.FinancialImpact.BillingImpactCode;
        request.FinancialImpact.CommissionImpactCode = string.IsNullOrWhiteSpace(request.FinancialImpact.CommissionImpactCode) ? profile.CommissionImpactCode : request.FinancialImpact.CommissionImpactCode;
    }

    private async Task ValidateOptionsAsync(Guid tenantId, string reasonCode, string priorityCode, PolicyEndorsementFinancialImpactInput financialImpact, CancellationToken cancellationToken)
    {
        var options = await _repository.GetOptionsAsync(tenantId, cancellationToken);
        RequireActiveOption(options, "Reason", reasonCode);
        RequireActiveOption(options, "Priority", priorityCode);
        RequireActiveOption(options, "BillingImpact", financialImpact.BillingImpactCode);
        RequireActiveOption(options, "CommissionImpact", financialImpact.CommissionImpactCode);
    }

    private static void RequireActiveOption(IReadOnlyList<PolicyEndorsementOptionDto> options, string groupCode, string? optionCode)
    {
        if (string.IsNullOrWhiteSpace(optionCode) || !options.Any(x => x.IsActive
            && string.Equals(x.OptionGroupCode, groupCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.OptionCode, optionCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The selected {groupCode} option is not active for the tenant.", nameof(optionCode));
    }

    private static void ApplyProfileDefaults(PolicyEndorsementTypeCatalogDto type, SavePolicyEndorsementDraftRequest request)
    {
        var profile = type.Profile!;
        request.CarrierMethodCode = string.IsNullOrWhiteSpace(request.CarrierMethodCode) ? profile.CarrierMethodCode : request.CarrierMethodCode;
        request.FinancialImpact.BillingImpactCode = string.IsNullOrWhiteSpace(request.FinancialImpact.BillingImpactCode) ? profile.BillingImpactCode : request.FinancialImpact.BillingImpactCode;
        request.FinancialImpact.CommissionImpactCode = string.IsNullOrWhiteSpace(request.FinancialImpact.CommissionImpactCode) ? profile.CommissionImpactCode : request.FinancialImpact.CommissionImpactCode;
    }

    public Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(request.GrantedPermissions, "ENDORSEMENT_APPROVE");
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        if (request.EndorsementRowVersion.Length == 0 || request.ApprovalRowVersion.Length == 0)
            throw new ArgumentException("Endorsement and approval row versions are required.", nameof(request));
        return _repository.DecideApprovalAsync(endorsementId, approvalId, request, cancellationToken);
    }

    public Task AssignApprovalAsync(Guid endorsementId, Guid approvalId, AssignPolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        if (request.ApprovalRowVersion.Length == 0) throw new ArgumentException("Approval row version is required.", nameof(request));
        return _repository.AssignApprovalAsync(endorsementId, approvalId, request, cancellationToken);
    }

    public Task<Guid> RequestInformationAsync(Guid endorsementId, RequestPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        if (request.EndorsementRowVersion.Length == 0) throw new ArgumentException("Endorsement row version is required.", nameof(request));
        return _repository.RequestInformationAsync(endorsementId, request, cancellationToken);
    }

    public Task RespondToInformationRequestAsync(Guid endorsementId, Guid informationRequestId, RespondPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        if (request.EndorsementRowVersion.Length == 0 || request.InformationRequestRowVersion.Length == 0)
            throw new ArgumentException("Endorsement and information request row versions are required.", nameof(request));
        return _repository.RespondToInformationRequestAsync(endorsementId, informationRequestId, request, cancellationToken);
    }

    public Task ResubmitInformationRequestAsync(Guid endorsementId, Guid informationRequestId, ResubmitPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ActorUserId.HasValue) throw new UnauthorizedAccessException("An authenticated user is required.");
        if (request.EndorsementRowVersion.Length == 0 || request.InformationRequestRowVersion.Length == 0)
            throw new ArgumentException("Endorsement and information request row versions are required.", nameof(request));
        return _repository.ResubmitInformationRequestAsync(endorsementId, informationRequestId, request, cancellationToken);
    }

    public async Task<Guid> ReverseAsync(Guid endorsementId, ReversePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(request.GrantedPermissions, "ENDORSEMENT_REVERSE");
        if (request.RowVersion.Length == 0) throw new ArgumentException("RowVersion is required.", nameof(request));
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate);
        var detail = await _repository.GetWorkflowDetailAsync(request.TenantId, endorsementId, cancellationToken)
            ?? throw new KeyNotFoundException("The endorsement was not found in the tenant.");
        if (!string.Equals(detail.Endorsement.Status, "Completed", StringComparison.OrdinalIgnoreCase) || detail.Endorsement.ReversedByEndorsementId.HasValue)
            throw new InvalidOperationException("Only an unreversed completed endorsement can be reversed.");
        if (!detail.Endorsement.PolicyId.HasValue || !request.ActorUserId.HasValue)
            throw new InvalidOperationException("The endorsement must be linked to a policy and authenticated user.");

        var type = await RequireTypeAsync(request.TenantId, detail.Endorsement.EndorsementType, cancellationToken);
        var profile = type.Profile ?? throw new InvalidOperationException("The endorsement type profile is not configured.");
        if (!profile.SupportsReversal)
            throw new InvalidOperationException("The endorsement type does not support reversal.");
        EnsureEffectiveDate(request.EffectiveDate, request.AllowBackdate && profile.SupportsBackdate);
        var options = await _repository.GetOptionsAsync(request.TenantId, cancellationToken);
        var reversalReason = options
            .Where(x => x.IsActive && string.Equals(x.OptionGroupCode, "Reason", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.SortOrder)
            .FirstOrDefault()?.OptionCode
            ?? throw new InvalidOperationException("No active endorsement reason is configured for the tenant.");

        var create = new CreatePolicyEndorsementTransactionRequest
        {
            TenantId = request.TenantId,
            PolicyId = detail.Endorsement.PolicyId.Value,
            EndorsementTypeCode = detail.Endorsement.EndorsementType,
            ReasonCode = reversalReason,
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
                BillingImpactCode = profile.BillingImpactCode,
                CommissionImpactCode = detail.Endorsement.CommissionImpactCode
            },
            Changes = detail.Changes.Select(ReverseChange).ToList(),
            CreatedByUserId = request.ActorUserId,
            AllowBackdate = request.AllowBackdate,
            ReversalOfEndorsementId = endorsementId,
            ReversalOfRowVersion = request.RowVersion
        };
        ValidateTransaction(create.TenantId, create.PolicyId, create.CreatedByUserId, create.Changes);
        if (!profile.IsPremiumBearing && (create.FinancialImpact.PremiumChange != 0 || create.FinancialImpact.AgencyFee != 0 || create.FinancialImpact.Taxes != 0))
            throw new InvalidOperationException("A non-premium-bearing endorsement cannot reverse a financial impact.");
        ApplyProfileDefaults(type, create);
        await ValidateOptionsAsync(create.TenantId, create.ReasonCode, create.PriorityCode, create.FinancialImpact, cancellationToken);
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
