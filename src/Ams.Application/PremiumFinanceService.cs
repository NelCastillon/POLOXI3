using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PremiumFinance;

namespace Ams.Application;

public sealed class PremiumFinanceService(
    IPremiumFinanceRepository repository,
    IPremiumFinanceProviderResolver providerResolver) : IPremiumFinanceService
{
    public Task<PremiumFinanceWorkbenchDto> GetWorkbenchAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return repository.GetWorkbenchAsync(tenantId, cancellationToken);
    }

    public Task<PremiumFinanceDetailDto?> GetDetailAsync(Guid tenantId, Guid premiumFinanceRequestId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return repository.GetDetailAsync(tenantId, premiumFinanceRequestId, cancellationToken);
    }

    public Task<PremiumFinanceSourceDto?> GetSourceAsync(Guid tenantId, string sourceTypeCode, Guid sourceId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return repository.GetSourceAsync(tenantId, sourceTypeCode, sourceId, cancellationToken);
    }

    public async Task<Guid> CreateRequestAsync(CreatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var sourceId = ResolveSourceId(request);
        var source = await repository.GetSourceAsync(request.TenantId, request.SourceTypeCode, sourceId, cancellationToken)
            ?? throw new InvalidOperationException("Premium finance source was not found for the tenant.");
        if (!source.IsEligible)
            throw new InvalidOperationException(source.IneligibilityReason ?? "The selected source is not eligible for premium financing.");

        var normalized = request with
        {
            SourceTypeCode = source.SourceTypeCode,
            QuoteId = source.QuoteId,
            PolicyId = source.PolicyId,
            RenewalId = source.RenewalId,
            SubmissionId = source.SubmissionId,
            AccountId = source.AccountId,
            CarrierId = source.CarrierId,
            ProducerUserId = source.ProducerUserId,
            InsuredName = source.InsuredName,
            AgencyName = source.AgencyName,
            ProducerName = source.ProducerName,
            CarrierName = source.CarrierName,
            PolicyOrQuoteNumber = source.PolicyOrQuoteNumber,
            LineOfBusiness = source.LineOfBusiness,
            EffectiveDate = source.EffectiveDate,
            PremiumAmount = source.PremiumAmount,
            TaxAmount = source.TaxAmount,
            FeeAmount = source.FeeAmount,
            CustomerEmail = request.CustomerEmail ?? source.CustomerEmail,
            CustomerPhone = request.CustomerPhone ?? source.CustomerPhone
        };
        return await repository.CreateRequestAsync(normalized, cancellationToken);
    }

    public async Task UpdateRequestAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireDetailAsync(request.TenantId, premiumFinanceRequestId, cancellationToken);
        if (IsTerminal(detail.Request.StatusCode)) throw new InvalidOperationException("Completed, declined, or cancelled requests cannot be edited.");
        if (request.RequestedDownPaymentAmount > detail.Request.TotalCostAmount) throw new InvalidOperationException("Requested down payment cannot exceed total premium, taxes, and fees.");
        if (request.PreferredFinanceCompanyId is not null)
            await RequireProviderAsync(request.TenantId, request.PreferredFinanceCompanyId.Value, null, cancellationToken);
        await repository.UpdateRequestAsync(premiumFinanceRequestId, Normalize(request), cancellationToken);
    }

    public async Task UpdateRequestStatusAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceStatusRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireDetailAsync(request.TenantId, premiumFinanceRequestId, cancellationToken);
        var target = request.StatusCode.Trim();
        await RequireReferenceCodeAsync(request.TenantId, "RequestStatus", target, cancellationToken);
        EnsureRequestTransition(detail.Request.StatusCode, target, detail);
        await repository.UpdateRequestStatusAsync(premiumFinanceRequestId, request with { StatusCode = target, Notes = Clean(request.Notes) }, cancellationToken);
    }

    public async Task<Guid> AddQuoteOptionAsync(AddPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireDetailAsync(request.TenantId, request.PremiumFinanceRequestId, cancellationToken);
        if (detail.Agreement is not null || IsTerminal(detail.Request.StatusCode)) throw new InvalidOperationException("Provider terms cannot be added after the request is completed.");
        await RequireProviderAsync(request.TenantId, request.FinanceCompanyId, p => p.SupportsQuotes, cancellationToken, "Provider does not support quote recording.");
        if (Math.Abs(request.DownPaymentAmount + request.AmountFinanced - detail.Request.TotalCostAmount) > 0.02m)
            throw new InvalidOperationException("Down payment plus amount financed must equal total premium, taxes, and fees.");
        if (request.PaymentAmount <= 0) throw new InvalidOperationException("Payment amount must be greater than zero.");
        if (request.FirstPaymentDate is not null && request.QuoteExpirationDate is not null && request.FirstPaymentDate < request.QuoteExpirationDate)
            throw new InvalidOperationException("First payment date cannot precede quote expiration.");
        return await repository.AddQuoteOptionAsync(request with { OptionName = request.OptionName.Trim(), TermsSummary = Clean(request.TermsSummary) }, cancellationToken);
    }

    public async Task SelectQuoteOptionAsync(Guid premiumFinanceRequestId, SelectPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireDetailAsync(request.TenantId, premiumFinanceRequestId, cancellationToken);
        if (detail.Agreement is not null || IsTerminal(detail.Request.StatusCode)) throw new InvalidOperationException("A financing option cannot be selected for a completed request.");
        if (!detail.QuoteOptions.Any(x => x.PremiumFinanceQuoteOptionId == request.PremiumFinanceQuoteOptionId && !x.IsSelected && x.StatusCode == "Received"))
            throw new InvalidOperationException("Available financing option was not found for this request.");
        await repository.SelectQuoteOptionAsync(premiumFinanceRequestId, request, cancellationToken);
    }

    public async Task<Guid> SubmitApplicationAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireDetailAsync(request.TenantId, request.PremiumFinanceRequestId, cancellationToken);
        if (detail.Agreement is not null) throw new InvalidOperationException("An agreement already exists for this request.");
        var selected = detail.QuoteOptions.SingleOrDefault(x => x.IsSelected)
            ?? throw new InvalidOperationException("A financing option must be selected before application submission.");
        if (selected.FinanceCompanyId != request.FinanceCompanyId) throw new InvalidOperationException("Selected option does not match the submitted provider.");
        var provider = await RequireProviderAsync(request.TenantId, request.FinanceCompanyId, p => p.SupportsApplications && p.SupportsAgreements, cancellationToken, "Provider does not support application and agreement recording.");
        var adapter = providerResolver.Resolve(provider.ProviderKey);
        var normalized = request with { AgreementNumber = request.AgreementNumber.Trim(), ProviderApplicationReference = Clean(request.ProviderApplicationReference) };
        var result = await adapter.SubmitApplicationAsync(normalized, cancellationToken);
        if (!result.IsSuccessful)
            throw new InvalidOperationException(result.Message ?? "Premium finance application submission failed.");
        return await repository.CreateAgreementAsync(normalized, cancellationToken);
    }

    public async Task UpdateAgreementAsync(UpdatePremiumFinanceAgreementRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireAgreementDetailAsync(request.TenantId, request.FinanceAgreementId, cancellationToken);
        if (request.ApplicationStatusCode is not null) await RequireReferenceCodeAsync(request.TenantId, "ApplicationStatus", request.ApplicationStatusCode, cancellationToken);
        if (request.SignatureStatusCode is not null) await RequireReferenceCodeAsync(request.TenantId, "SignatureStatus", request.SignatureStatusCode, cancellationToken);
        if (request.FundingStatusCode is not null) await RequireReferenceCodeAsync(request.TenantId, "FundingStatus", request.FundingStatusCode, cancellationToken);
        if (request.AccountStatusCode is not null) await RequireReferenceCodeAsync(request.TenantId, "AccountStatus", request.AccountStatusCode, cancellationToken);
        if (request.StatusCode is not null) await RequireReferenceCodeAsync(request.TenantId, "AgreementStatus", request.StatusCode, cancellationToken);
        if (request.PayoffAmount is not null && request.PayoffGoodThroughDate is null) throw new InvalidOperationException("Payoff good-through date is required with a payoff amount.");
        if (request.FundedDate is not null && request.FundingStatusCode is not null && request.FundingStatusCode != "Funded") throw new InvalidOperationException("Funded date requires Funded status.");
        if (detail.Agreement?.StatusCode is "PaidOff" or "Cancelled") throw new InvalidOperationException("Terminal agreements cannot be modified.");
        await repository.UpdateAgreementAsync(request with { Notes = Clean(request.Notes) }, cancellationToken);
    }

    public async Task ReplacePaymentScheduleAsync(ReplacePremiumFinancePaymentScheduleRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await RequireAgreementDetailAsync(request.TenantId, request.FinanceAgreementId, cancellationToken);
        var agreement = detail.Agreement!;
        await RequireProviderAsync(request.TenantId, agreement.FinanceCompanyId, p => p.SupportsPaymentSchedules, cancellationToken, "Provider does not support payment schedule recording.");
        if (request.Items.Select(x => x.InstallmentNumber).Distinct().Count() != request.Items.Count)
            throw new InvalidOperationException("Payment schedule installment numbers must be unique.");
        if (request.Items.Select(x => x.DueDate).Distinct().Count() != request.Items.Count) throw new InvalidOperationException("Payment schedule due dates must be unique.");
        if (agreement.PaymentCount is not null && request.Items.Count != agreement.PaymentCount) throw new InvalidOperationException("Payment schedule count must match the selected financing option.");
        foreach (var item in request.Items)
        {
            await RequireReferenceCodeAsync(request.TenantId, "PaymentStatus", item.StatusCode, cancellationToken);
            if (item.PaidAmount > item.ScheduledAmount) throw new InvalidOperationException($"Installment {item.InstallmentNumber} paid amount cannot exceed scheduled amount.");
            if (item.StatusCode == "Paid" && (item.PaidAmount is null || item.PaidDate is null)) throw new InvalidOperationException($"Paid installment {item.InstallmentNumber} requires paid amount and paid date.");
        }
        await repository.ReplacePaymentScheduleAsync(request, cancellationToken);
    }

    public async Task<Guid> AddActivityAsync(AddPremiumFinanceActivityRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        await RequireReferenceCodeAsync(request.TenantId, "ActivityType", request.ActivityTypeCode, cancellationToken);
        await RequireConsistentParentsAsync(request.TenantId, request.PremiumFinanceRequestId, request.FinanceAgreementId, cancellationToken);
        return await repository.AddActivityAsync(request with { ActivityTypeCode = request.ActivityTypeCode.Trim(), Subject = request.Subject.Trim(), Notes = Clean(request.Notes), ProviderReference = Clean(request.ProviderReference) }, cancellationToken);
    }

    public async Task<Guid> LinkDocumentAsync(LinkPremiumFinanceDocumentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        await RequireReferenceCodeAsync(request.TenantId, "DocumentRole", request.DocumentRoleCode, cancellationToken);
        await RequireConsistentParentsAsync(request.TenantId, request.PremiumFinanceRequestId, request.FinanceAgreementId, cancellationToken);
        return await repository.LinkDocumentAsync(request with { DocumentRoleCode = request.DocumentRoleCode.Trim() }, cancellationToken);
    }

    public Task<Guid> UpsertProviderAsync(UpsertPremiumFinanceProviderRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        if (!request.IntegrationLevelCode.Equals("Manual", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.ProviderKey))
            throw new InvalidOperationException("A provider adapter key is required for assisted or API integrations.");
        var normalized = request with { CompanyCode = request.CompanyCode.Trim().ToUpperInvariant(), CompanyName = request.CompanyName.Trim(), ContactName = Clean(request.ContactName), EmailAddress = Clean(request.EmailAddress), PhoneNumber = Clean(request.PhoneNumber), ProviderKey = Clean(request.ProviderKey), WebsiteUrl = Clean(request.WebsiteUrl), PortalUrl = Clean(request.PortalUrl), ExternalProviderId = Clean(request.ExternalProviderId), RemittanceInstructions = Clean(request.RemittanceInstructions) };
        return repository.UpsertProviderAsync(normalized, cancellationToken);
    }

    public async Task CancelRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant(request.TenantId);
        var detail = await repository.GetDetailAsync(request.TenantId, request.PremiumFinanceRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Premium finance request was not found for the tenant.");
        if (detail.Request.StatusCode is "Active" or "Cancelled" or "Declined") throw new InvalidOperationException("Premium finance request cannot be cancelled in its current status.");
        var workspace = await repository.GetWorkbenchAsync(request.TenantId, cancellationToken);
        var financeCompanyId = detail.Agreement?.FinanceCompanyId ?? detail.Request.PreferredFinanceCompanyId;
        var provider = financeCompanyId is null ? null : workspace.Providers.SingleOrDefault(x => x.FinanceCompanyId == financeCompanyId);
        var adapter = providerResolver.Resolve(provider?.ProviderKey);
        await adapter.CancelRequestAsync(request, cancellationToken);
        await repository.CancelRequestAsync(request, cancellationToken);
    }

    private async Task<PremiumFinanceDetailDto> RequireDetailAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        => await repository.GetDetailAsync(tenantId, requestId, cancellationToken) ?? throw new InvalidOperationException("Premium finance request was not found for the tenant.");

    private async Task<PremiumFinanceDetailDto> RequireAgreementDetailAsync(Guid tenantId, Guid agreementId, CancellationToken cancellationToken)
    {
        var workbench = await repository.GetWorkbenchAsync(tenantId, cancellationToken);
        var agreement = workbench.Agreements.SingleOrDefault(x => x.FinanceAgreementId == agreementId) ?? throw new InvalidOperationException("Premium finance agreement was not found for the tenant.");
        return await RequireDetailAsync(tenantId, agreement.PremiumFinanceRequestId!.Value, cancellationToken);
    }

    private async Task<PremiumFinanceProviderDto> RequireProviderAsync(Guid tenantId, Guid providerId, Func<PremiumFinanceProviderDto, bool>? capability, CancellationToken cancellationToken, string? capabilityMessage = null)
    {
        var provider = (await repository.GetWorkbenchAsync(tenantId, cancellationToken)).Providers.SingleOrDefault(x => x.FinanceCompanyId == providerId && x.IsActive)
            ?? throw new InvalidOperationException("Premium finance provider was not found for the tenant.");
        if (capability is not null && !capability(provider)) throw new InvalidOperationException(capabilityMessage ?? "Provider does not support this operation.");
        return provider;
    }

    private async Task RequireReferenceCodeAsync(Guid tenantId, string group, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || !(await repository.GetWorkbenchAsync(tenantId, cancellationToken)).ReferenceOptions.Any(x => x.OptionGroupCode == group && x.OptionCode.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid {group} value.");
    }

    private async Task RequireConsistentParentsAsync(Guid tenantId, Guid? requestId, Guid? agreementId, CancellationToken cancellationToken)
    {
        if (requestId is null && agreementId is null) throw new InvalidOperationException("Request or agreement is required.");
        PremiumFinanceDetailDto? detail = requestId is null ? null : await RequireDetailAsync(tenantId, requestId.Value, cancellationToken);
        if (agreementId is not null)
        {
            var agreementDetail = await RequireAgreementDetailAsync(tenantId, agreementId.Value, cancellationToken);
            if (detail is not null && agreementDetail.Request.PremiumFinanceRequestId != detail.Request.PremiumFinanceRequestId) throw new InvalidOperationException("Agreement does not belong to the selected request.");
        }
    }

    private static void EnsureRequestTransition(string current, string target, PremiumFinanceDetailDto detail)
    {
        if (current.Equals(target, StringComparison.OrdinalIgnoreCase)) return;
        var allowed = current switch
        {
            "Draft" => new[] { "OptionsRequested", "Cancelled" },
            "OptionsRequested" => new[] { "OptionsReceived", "Cancelled" },
            "OptionsReceived" => new[] { "OptionSelected", "Cancelled" },
            "OptionSelected" => new[] { "ApplicationSubmitted", "Cancelled" },
            "ApplicationSubmitted" => new[] { "PendingSignature", "PendingApproval", "Approved", "Declined", "Cancelled" },
            "PendingSignature" => new[] { "PendingApproval", "Declined", "Cancelled" },
            "PendingApproval" => new[] { "Approved", "Declined", "Cancelled" },
            "Approved" => new[] { "Active", "Cancelled" },
            _ => Array.Empty<string>()
        };
        if (!allowed.Contains(target, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"Status cannot change from {current} to {target}.");
        if (target == "OptionSelected" && detail.Request.SelectedQuoteOptionId is null) throw new InvalidOperationException("Select a financing option before changing to Option Selected.");
        if (target is "ApplicationSubmitted" or "PendingSignature" or "PendingApproval" or "Approved" or "Active" && detail.Agreement is null) throw new InvalidOperationException("An agreement is required for the selected status.");
    }

    private static UpdatePremiumFinanceRequest Normalize(UpdatePremiumFinanceRequest request) => request with { CustomerEmail = Clean(request.CustomerEmail), CustomerPhone = Clean(request.CustomerPhone), Notes = Clean(request.Notes) };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsTerminal(string status) => status is "Active" or "Declined" or "Cancelled";

    private static Guid ResolveSourceId(CreatePremiumFinanceRequest request)
        => request.SourceTypeCode.Trim().ToLowerInvariant() switch
        {
            "quote" => request.QuoteId ?? throw new InvalidOperationException("Quote is required."),
            "policy" => request.PolicyId ?? throw new InvalidOperationException("Policy is required."),
            "renewal" => request.RenewalId ?? throw new InvalidOperationException("Renewal is required."),
            _ => throw new InvalidOperationException("Source type must be Quote, Policy, or Renewal.")
        };

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Tenant is required.");
    }
}
