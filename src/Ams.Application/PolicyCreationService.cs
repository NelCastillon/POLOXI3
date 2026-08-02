using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;

namespace Ams.Application;

public sealed class PolicyCreationService : IPolicyCreationService
{
    private readonly IPolicyCreationRepository _repository;

    public PolicyCreationService(IPolicyCreationRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy creation requires a tenant.");
        }

        if (request.PolicyBindTransactionId == Guid.Empty)
        {
            throw new InvalidOperationException("Policy creation requires a confirmed bind request.");
        }

        return _repository.CreatePolicyFromConfirmedBindAsync(request, cancellationToken);
    }

    public Task<BinderReviewDto?> GetBinderReviewAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (policyBindTransactionId == Guid.Empty || tenantId == Guid.Empty) throw new InvalidOperationException("Binder review requires bind request and tenant identifiers.");
        return _repository.GetBinderReviewAsync(policyBindTransactionId, tenantId, cancellationToken);
    }

    public Task<BinderReviewDto> SaveBinderReviewAsync(Guid policyBindTransactionId, UpsertBinderReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (policyBindTransactionId == Guid.Empty || request.TenantId == Guid.Empty) throw new InvalidOperationException("Binder review requires bind request and tenant identifiers.");
        return _repository.SaveBinderReviewAsync(policyBindTransactionId, request, cancellationToken);
    }

    public Task DecideBinderReviewAsync(Guid policyBindTransactionId, DecideBinderReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (policyBindTransactionId == Guid.Empty || request.TenantId == Guid.Empty) throw new InvalidOperationException("Binder review decision requires bind request and tenant identifiers.");
        return _repository.DecideBinderReviewAsync(policyBindTransactionId, request, cancellationToken);
    }

    public Task<PolicyGenerationRequestDto> QueuePolicyGenerationAsync(Guid policyBindTransactionId, QueuePolicyGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (policyBindTransactionId == Guid.Empty || request.TenantId == Guid.Empty) throw new InvalidOperationException("Policy generation requires bind request and tenant identifiers.");
        return _repository.QueuePolicyGenerationAsync(policyBindTransactionId, request, cancellationToken);
    }

    public Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Manual policy options require a tenant.");
        }

        return _repository.GetManualPolicyOptionsAsync(tenantId, cancellationToken);
    }

    public Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Manual policy draft requires a tenant.");
        }

        if (request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException("Manual policy draft must be attached to an account.");
        }

        if (request.CurrentStep is < 1 or > 9)
        {
            throw new InvalidOperationException("Manual policy draft step is invalid.");
        }

        return _repository.SaveManualPolicyDraftAsync(draftId, request, cancellationToken);
    }

    public Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || accountId == Guid.Empty || draftId == Guid.Empty)
        {
            throw new InvalidOperationException("Manual policy draft lookup requires tenant, account, and draft identifiers.");
        }

        return _repository.GetManualPolicyDraftAsync(tenantId, accountId, draftId, cancellationToken);
    }

    public async Task<ManualPolicyValidationResultDto> ValidateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default)
    {
        NormalizeManualPolicyLines(request);
        var result = new ManualPolicyValidationResultDto();
        AddManualPolicyValidationMessages(request, result);

        if (request.TenantId != Guid.Empty && request.AccountId != Guid.Empty && !string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            var duplicates = await _repository.FindManualPolicyDuplicatesAsync(request, cancellationToken);
            result.Duplicates.AddRange(duplicates);
            if (duplicates.Any(d => string.Equals(d.Classification, "ExactDuplicate", StringComparison.OrdinalIgnoreCase)))
            {
                result.BlockingErrors.Add("A matching policy already exists for this account, carrier, policy number, and term.");
            }
            else if (duplicates.Count > 0 && !request.OverridePossibleDuplicate)
            {
                result.Warnings.Add("Possible duplicate policies were found. Review them before creating the policy record.");
            }
        }

        return result;
    }

    public async Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateManualPolicyAsync(request, cancellationToken);
        if (validation.BlockingErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", validation.BlockingErrors));
        }

        if (validation.Duplicates.Count > 0 && !request.OverridePossibleDuplicate)
        {
            var hasExactDuplicate = validation.Duplicates.Any(d => string.Equals(d.Classification, "ExactDuplicate", StringComparison.OrdinalIgnoreCase));
            if (hasExactDuplicate)
            {
                throw new InvalidOperationException("A matching policy already exists.");
            }
        }

        request.PolicySourceCode = string.IsNullOrWhiteSpace(request.PolicySourceCode) ? "ManualExistingPolicy" : request.PolicySourceCode.Trim();
        request.PolicyStatus = string.IsNullOrWhiteSpace(request.PolicyStatus) ? "PendingVerification" : request.PolicyStatus.Trim();
        request.TermStatus = string.IsNullOrWhiteSpace(request.TermStatus) ? "Active" : request.TermStatus.Trim();
        request.TransactionTypeCode = string.IsNullOrWhiteSpace(request.TransactionTypeCode) ? "Conversion" : request.TransactionTypeCode.Trim();
        request.BillingTypeCode = string.IsNullOrWhiteSpace(request.BillingTypeCode) ? "DirectBill" : request.BillingTypeCode.Trim();
        request.DataCompletenessCode = string.IsNullOrWhiteSpace(request.DataCompletenessCode) ? "Partial" : request.DataCompletenessCode.Trim();
        request.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "USD" : request.CurrencyCode.Trim();
        NormalizeManualPolicyLines(request);

        return await _repository.CreateManualPolicyAsync(request, cancellationToken);
    }

    private static void NormalizeManualPolicyLines(CreateManualPolicyRequest request)
    {
        request.PolicyLines = request.PolicyLines
            .Where(line => !string.IsNullOrWhiteSpace(line.LineOfBusinessCode) || !string.IsNullOrWhiteSpace(line.LineOfBusinessName) || line.LineOfBusinessId.HasValue)
            .OrderBy(line => line.SortOrder)
            .Select((line, index) => new ManualPolicyLineRequest
            {
                LineOfBusinessId = line.LineOfBusinessId,
                LineOfBusinessCode = string.IsNullOrWhiteSpace(line.LineOfBusinessCode) ? line.LineOfBusinessName.Trim() : line.LineOfBusinessCode.Trim(),
                LineOfBusinessName = string.IsNullOrWhiteSpace(line.LineOfBusinessName) ? line.LineOfBusinessCode.Trim() : line.LineOfBusinessName.Trim(),
                PolicyLineStatusCode = string.IsNullOrWhiteSpace(line.PolicyLineStatusCode) ? "Active" : line.PolicyLineStatusCode.Trim(),
                WrittenPremium = line.WrittenPremium,
                CoverageSummary = line.CoverageSummary,
                LimitsSummary = line.LimitsSummary,
                DeductibleSummary = line.DeductibleSummary,
                SortOrder = index + 1
            })
            .ToList();

        if (request.PolicyLines.Count == 0 && !string.IsNullOrWhiteSpace(request.LineOfBusiness))
        {
            request.PolicyLines.Add(new ManualPolicyLineRequest
            {
                LineOfBusinessCode = request.LineOfBusiness.Trim(),
                LineOfBusinessName = request.LineOfBusiness.Trim(),
                PolicyLineStatusCode = string.IsNullOrWhiteSpace(request.TermStatus) ? "Active" : request.TermStatus.Trim(),
                WrittenPremium = request.WrittenPremium,
                CoverageSummary = request.CoverageSummary,
                LimitsSummary = request.LimitsSummary,
                DeductibleSummary = request.DeductibleSummary,
                SortOrder = 1
            });
        }

        request.LineOfBusiness = request.PolicyLines.Count switch
        {
            0 => request.LineOfBusiness.Trim(),
            1 => request.PolicyLines[0].LineOfBusinessName,
            _ => "Package"
        };
    }

    private static void AddManualPolicyValidationMessages(CreateManualPolicyRequest request, ManualPolicyValidationResultDto result)
    {
        if (request.TenantId == Guid.Empty)
            result.BlockingErrors.Add("Tenant is required.");
        if (request.AccountId == Guid.Empty)
            result.BlockingErrors.Add("Policy must be attached to an account.");
        if (request.CarrierId == Guid.Empty)
            result.BlockingErrors.Add("Carrier is required.");
        if (string.IsNullOrWhiteSpace(request.PolicyNumber))
            result.BlockingErrors.Add("Policy number is required.");
        if (string.IsNullOrWhiteSpace(request.ManualReasonCode))
            result.BlockingErrors.Add("Manual entry reason is required.");
        if (request.PolicyLines.Count == 0)
            result.BlockingErrors.Add("At least one policy line is required.");
        if (request.PolicyLines.Any(line => string.IsNullOrWhiteSpace(line.LineOfBusinessCode) || string.IsNullOrWhiteSpace(line.LineOfBusinessName)))
            result.BlockingErrors.Add("Each policy line requires a line of business.");
        if (request.PolicyLines.Any(line => line.WrittenPremium is < 0))
            result.BlockingErrors.Add("Policy line premiums cannot be negative.");
        if (request.PolicyLines.Select(line => line.LineOfBusinessCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.PolicyLines.Count)
            result.BlockingErrors.Add("Duplicate policy lines are not allowed.");
        if (string.IsNullOrWhiteSpace(request.NamedInsured))
            result.BlockingErrors.Add("Named insured is required.");
        if (request.EffectiveDate == default)
            result.BlockingErrors.Add("Effective date is required.");
        if (request.ExpirationDate == default)
            result.BlockingErrors.Add("Expiration date is required.");
        if (request.EffectiveDate != default && request.ExpirationDate != default && request.ExpirationDate <= request.EffectiveDate)
            result.BlockingErrors.Add("Effective date must precede expiration date.");
        if (request.WrittenPremium is < 0 || request.AnnualizedPremium is < 0 || request.Taxes is < 0 || request.Fees is < 0 || request.Surcharges is < 0 || request.TotalCost is < 0)
            result.BlockingErrors.Add("Premium, taxes, fees, surcharges, and total cost cannot be negative.");

        if (!request.HasSupportingDocument)
            result.Warnings.Add("No declarations page, binder, or carrier confirmation is attached.");
        if (string.Equals(request.DataCompletenessCode, "Partial", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("Coverage details are incomplete and the policy will be marked Partial.");
        if (request.CommissionRate is null or <= 0)
            result.Warnings.Add("Commission has not been entered and will remain Estimated.");
        if (request.EffectiveDate != default && request.EffectiveDate < DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-1)))
            result.Warnings.Add("The effective date is significantly backdated and may require elevated permission.");
    }
}
