using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyLifecycle;

namespace Ams.Application;

public sealed class PolicyLifecycleService : IPolicyLifecycleService
{
    private readonly IPolicyLifecycleRepository _repository;

    public PolicyLifecycleService(IPolicyLifecycleRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PolicyLifecycleOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle options require a tenant.");
        }

        return _repository.GetOptionsAsync(tenantId, cancellationToken);
    }

    public Task<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>> GetWorkbenchAsync(Guid tenantId, string? mode = null, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle workbench requires a tenant.");
        }

        return _repository.GetWorkbenchAsync(tenantId, NormalizeMode(mode), cancellationToken);
    }

    public Task<PolicyLifecycleDetailDto?> GetDetailAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || policyId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle detail requires tenant and policy identifiers.");
        }

        return _repository.GetDetailAsync(tenantId, policyId, cancellationToken);
    }

    public Task<Guid> CreateTransactionAsync(CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(request);
        NormalizeTransaction(request);
        return _repository.CreateTransactionAsync(request, cancellationToken);
    }

    public Task TransitionTransactionAsync(Guid policyTransactionId, TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (policyTransactionId == Guid.Empty || request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle transition requires transaction and tenant identifiers.");
        }

        if (string.IsNullOrWhiteSpace(request.ToStatusCode))
        {
            throw new InvalidOperationException("Policy lifecycle transition requires a target status.");
        }

        request.ToStatusCode = request.ToStatusCode.Trim();
        request.ReasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? null : request.ReasonCode.Trim();
        request.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        return _repository.TransitionTransactionAsync(policyTransactionId, request, cancellationToken);
    }

    private static string? NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "endorsement" or "endorsements" => "endorsements",
            "cancellation" or "cancellations" or "reinstatement" or "reinstatements" => "cancellations",
            "policy" or "policies" => "policies",
            var value => value
        };
    }

    private static void ValidateTransaction(CreatePolicyLifecycleTransactionRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle transaction requires a tenant.");
        }

        if (request.PolicyId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy lifecycle transaction requires a policy.");
        }

        if (string.IsNullOrWhiteSpace(request.TransactionTypeCode))
        {
            throw new InvalidOperationException("Policy lifecycle transaction type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TransactionStatusCode))
        {
            throw new InvalidOperationException("Policy lifecycle transaction status is required.");
        }

        if (request.EffectiveDate == default)
        {
            throw new InvalidOperationException("Policy lifecycle transaction effective date is required.");
        }

        if (request.ExpirationDate.HasValue && request.ExpirationDate.Value <= request.EffectiveDate)
        {
            throw new InvalidOperationException("Policy lifecycle transaction expiration date must be after the effective date.");
        }

        if (request.TransactionTypeCode.Equals("Cancellation", StringComparison.OrdinalIgnoreCase) && request.TransactionStatusCode.Equals("Completed", StringComparison.OrdinalIgnoreCase) && request.Documents.Count == 0)
        {
            throw new InvalidOperationException("Completed cancellation transactions require linked documentation.");
        }

        if (request.TransactionTypeCode.Equals("Reinstatement", StringComparison.OrdinalIgnoreCase) && request.TransactionStatusCode.Equals("Completed", StringComparison.OrdinalIgnoreCase) && request.Documents.Count == 0)
        {
            throw new InvalidOperationException("Completed reinstatement transactions require linked documentation.");
        }

        if (request.LineChanges.Any(line => string.IsNullOrWhiteSpace(line.LineOfBusinessCode) || string.IsNullOrWhiteSpace(line.LineOfBusinessName) || string.IsNullOrWhiteSpace(line.ChangeTypeCode)))
        {
            throw new InvalidOperationException("Each policy transaction line change requires a line of business and change type.");
        }

        if (request.Documents.Any(document => string.IsNullOrWhiteSpace(document.DocumentRoleCode) || string.IsNullOrWhiteSpace(document.DocumentTitle)))
        {
            throw new InvalidOperationException("Each policy transaction document requires a role and title.");
        }
    }

    private static void NormalizeTransaction(CreatePolicyLifecycleTransactionRequest request)
    {
        request.TransactionTypeCode = request.TransactionTypeCode.Trim();
        request.TransactionStatusCode = request.TransactionStatusCode.Trim();
        request.ReasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? null : request.ReasonCode.Trim();
        request.SourceCode = string.IsNullOrWhiteSpace(request.SourceCode) ? "PolicyLifecycle" : request.SourceCode.Trim();
        request.ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim();
        request.CarrierTransactionNumber = string.IsNullOrWhiteSpace(request.CarrierTransactionNumber) ? null : request.CarrierTransactionNumber.Trim();
        request.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        request.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        foreach (var line in request.LineChanges)
        {
            line.LineOfBusinessCode = line.LineOfBusinessCode.Trim();
            line.LineOfBusinessName = line.LineOfBusinessName.Trim();
            line.ChangeTypeCode = line.ChangeTypeCode.Trim();
        }

        foreach (var document in request.Documents)
        {
            document.DocumentRoleCode = document.DocumentRoleCode.Trim();
            document.DocumentTitle = document.DocumentTitle.Trim();
            document.DocumentNumber = string.IsNullOrWhiteSpace(document.DocumentNumber) ? null : document.DocumentNumber.Trim();
            document.FileName = string.IsNullOrWhiteSpace(document.FileName) ? null : document.FileName.Trim();
            document.StorageUri = string.IsNullOrWhiteSpace(document.StorageUri) ? null : document.StorageUri.Trim();
        }
    }
}
