using System.Globalization;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.Opportunities;
using Ams.Application.Features.Submissions;
using Ams.Application.Features.SubmissionIntake;

namespace Ams.Application;

/// <summary>
/// Normalizes direct submission intake into the mandatory enterprise chain:
/// Account (Prospect when new) -> Opportunity (Submission Preparation) -> Submission.
/// Runs the Account Match engine before creating any new account to avoid duplicates.
/// </summary>
public sealed class SubmissionIntakeService : ISubmissionIntakeService
{
    private readonly ISubmissionIntakeRepository _intakeRepository;
    private readonly IAccountMatchingService _matchingService;
    private readonly IAccountRepository _accountRepository;
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IOpportunityForecastCategoryService _forecastCategoryService;
    private readonly ISubmissionReferenceOptionRepository _referenceOptionRepository;
    private readonly IAccountTypeRepository _accountTypeRepository;
    private readonly IAccountReferenceOptionRepository _accountReferenceOptionRepository;

    public SubmissionIntakeService(
        ISubmissionIntakeRepository intakeRepository,
        IAccountMatchingService matchingService,
        IAccountRepository accountRepository,
        IOpportunityRepository opportunityRepository,
        ISubmissionRepository submissionRepository,
        IOpportunityForecastCategoryService forecastCategoryService,
        ISubmissionReferenceOptionRepository referenceOptionRepository,
        IAccountTypeRepository accountTypeRepository,
        IAccountReferenceOptionRepository accountReferenceOptionRepository)
    {
        _intakeRepository = intakeRepository;
        _matchingService = matchingService;
        _accountRepository = accountRepository;
        _opportunityRepository = opportunityRepository;
        _submissionRepository = submissionRepository;
        _forecastCategoryService = forecastCategoryService;
        _referenceOptionRepository = referenceOptionRepository;
        _accountTypeRepository = accountTypeRepository;
        _accountReferenceOptionRepository = accountReferenceOptionRepository;
    }

    public Task<PagedResult<SubmissionIntakeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? source, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _intakeRepository.SearchAsync(tenantId, searchTerm, status, source, pageNumber, pageSize, cancellationToken);

    public Task<SubmissionIntakeDto?> GetAsync(Guid intakeId, CancellationToken cancellationToken = default)
        => _intakeRepository.GetByIdAsync(intakeId, cancellationToken);

    public Task<Guid> CaptureAsync(CreateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
        => _intakeRepository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid intakeId, UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
        => _intakeRepository.UpdateAsync(intakeId, request, cancellationToken);

    public async Task<AccountMatchResult> PreviewMatchAsync(Guid intakeId, CancellationToken cancellationToken = default)
    {
        var intake = await _intakeRepository.GetByIdAsync(intakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission intake '{intakeId}' was not found.");
        return await _matchingService.MatchAsync(BuildCriteria(intake), cancellationToken);
    }

    public async Task<PromoteSubmissionIntakeResult> PromoteAsync(Guid intakeId, PromoteSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        var intake = await _intakeRepository.GetByIdAsync(intakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission intake '{intakeId}' was not found.");

        if (intake.AccountId.HasValue && intake.SubmissionId.HasValue)
        {
            return new PromoteSubmissionIntakeResult
            {
                IntakeId = intakeId,
                AccountId = intake.AccountId.Value,
                AccountCreated = false,
                OpportunityId = intake.OpportunityId ?? Guid.Empty,
                SubmissionId = intake.SubmissionId.Value,
                MatchScore = intake.MatchScore,
                Message = $"Submission intake '{intake.IntakeNumber}' was already promoted; the existing Submission was returned."
            };
        }

        var tenantId = request.TenantId != Guid.Empty ? request.TenantId : intake.TenantId;
        if (request.LobId == Guid.Empty)
        {
            throw new InvalidOperationException("A canonical tenant Line of Business is required before intake promotion.");
        }

        // 1. Resolve the Account context. No submission may exist without one.
        var match = await _matchingService.MatchAsync(BuildCriteria(intake), cancellationToken);
        var matchedAccountId = match.ExistingAccountId ?? Guid.Empty;

        Guid accountId;
        var accountCreated = false;

        if (request.AccountId.HasValue)
        {
            // Producer explicitly chose an existing account from the candidate list.
            // Verify it exists and belongs to the same tenant so the submission is never
            // attached to another tenant's account (enterprise tenant isolation).
            var chosen = await _accountRepository.GetByIdAsync(request.AccountId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Selected account '{request.AccountId.Value}' was not found.");
            if (chosen.TenantId != tenantId)
            {
                throw new InvalidOperationException("Selected account belongs to a different tenant and cannot be used for this intake.");
            }
            accountId = chosen.AccountId;
        }
        else if (!request.CreateNewAccount && match.IsAutoMatch && match.ExistingAccountId.HasValue)
        {
            // High-confidence auto-match (>= 95). Reuse the existing account.
            accountId = match.ExistingAccountId.Value;
        }
        else
        {
            // No confident match (or forced new) -> create Prospect account.
            accountId = await CreateProspectAccountAsync(intake, tenantId, request.ProcessedByUserId, cancellationToken);
            accountCreated = true;
        }

        if (matchedAccountId == Guid.Empty)
        {
            matchedAccountId = accountId;
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var accountName = ResolveAccountName(intake, stamp);
        var forecastCategory = await ResolveDefaultForecastCategoryAsync(tenantId, cancellationToken);

        // 2. Create the Opportunity (mandatory sales/work container).
        var opportunityId = await _opportunityRepository.CreateAsync(new CreateOpportunityRequest
        {
            TenantId = tenantId,
            AccountId = accountId,
            OpportunityName = $"{accountName} - {intake.LineOfBusiness}",
            EstimatedAmount = intake.EstimatedPremium ?? 0,
            OwnerUserId = intake.AssignedToUserId,
            CloseDate = (intake.RequestedEffectiveDate ?? DateTime.UtcNow).AddDays(request.OpportunityCloseDays),
            WinProbability = request.OpportunityWinProbability,
            ForecastCategoryCode = forecastCategory,
            CreatedByUserId = request.ProcessedByUserId
        }, cancellationToken);

        await _opportunityRepository.UpsertLineAsync(new UpsertOpportunityLineRequest
        {
            TenantId = tenantId,
            OpportunityId = opportunityId,
            LobId = request.LobId,
            LineOfBusiness = intake.LineOfBusiness,
            EstPremium = intake.EstimatedPremium ?? 0,
            Priority = request.OpportunityLinePriorityCode,
            Status = request.OpportunityLineStatusCode,
            IsPrimary = true,
            TargetEffectiveDate = intake.RequestedEffectiveDate,
            AssignedToUserId = intake.AssignedToUserId,
            UserId = request.ProcessedByUserId
        }, cancellationToken);

        // 3. Create the Submission under the Opportunity (never orphaned).
        var effectiveDate = intake.RequestedEffectiveDate ?? DateTime.UtcNow.Date;
        var referenceOptions = await _referenceOptionRepository.GetAllAsync(tenantId, cancellationToken: cancellationToken);
        var priority = referenceOptions.FirstOrDefault(option => option.OptionGroup == "SubmissionPriority" && option.IsDefault && option.IsActive)?.OptionCode
            ?? throw new InvalidOperationException("A default submission priority must be configured before intake can be promoted.");
        var riskState = referenceOptions.FirstOrDefault(option => option.OptionGroup == "RiskState" && option.IsActive && string.Equals(option.OptionCode, intake.State, StringComparison.OrdinalIgnoreCase))?.OptionCode
            ?? referenceOptions.FirstOrDefault(option => option.OptionGroup == "RiskState" && option.IsDefault && option.IsActive)?.OptionCode
            ?? throw new InvalidOperationException("A valid intake or default risk state must be configured before intake can be promoted.");
        var submissionId = await _submissionRepository.CreateAsync(new CreateSubmissionRequest(
            TenantId: tenantId,
            AccountId: accountId,
            OpportunityId: opportunityId,
            Priority: priority,
            EffectiveDate: effectiveDate,
            ExpirationDate: effectiveDate.AddMonths(request.SubmissionTermMonths),
            TargetPremium: intake.EstimatedPremium,
            AssignedToUserId: intake.AssignedToUserId,
            RiskState: riskState,
            NamedInsured: null,
            CsrUserId: null,
            Description: null,
            InternalNotes: null,
            IsRush: false,
            CreatedByUserId: request.ProcessedByUserId), cancellationToken);

        // 4. Record the normalization outcome on the staged intake.
        await _intakeRepository.MarkPromotedAsync(intakeId, match.MatchScore, matchedAccountId, accountId, opportunityId, submissionId, request.ProcessedByUserId, cancellationToken);

        return new PromoteSubmissionIntakeResult
        {
            IntakeId = intakeId,
            AccountId = accountId,
            AccountCreated = accountCreated,
            OpportunityId = opportunityId,
            SubmissionId = submissionId,
            MatchScore = match.MatchScore,
            Message = accountCreated
                ? $"Created Prospect account and normalized intake into Opportunity + Submission."
                : $"Matched existing account ({match.MatchScore}% confidence) and normalized intake into Opportunity + Submission."
        };
    }

    public Task UpdateStatusAsync(Guid intakeId, UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken = default)
        => _intakeRepository.UpdateStatusAsync(intakeId, request, cancellationToken);

    public Task DeleteAsync(Guid intakeId, Guid? userId = null, CancellationToken cancellationToken = default)
        => _intakeRepository.DeleteAsync(intakeId, userId, cancellationToken);

    private async Task<Guid> CreateProspectAccountAsync(SubmissionIntakeDto intake, Guid tenantId, Guid? userId, CancellationToken cancellationToken)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var accountTypes = await _accountTypeRepository.SearchAsync(tenantId, null, 1, 500, cancellationToken);
        var accountType = accountTypes.Items.FirstOrDefault(option => option.IsActive && option.IsDefault)
            ?? throw new InvalidOperationException("A default active account type must be configured before intake promotion.");
        var accountOptions = await _accountReferenceOptionRepository.GetAllAsync(tenantId, ct: cancellationToken);
        var status = accountOptions.FirstOrDefault(option => option.OptionGroup == "Status" && option.IsActive && option.IsDefault)?.OptionCode
            ?? throw new InvalidOperationException("A default active account status must be configured before intake promotion.");
        var lifecycleStage = accountOptions.FirstOrDefault(option => option.OptionGroup == "LifecycleStage" && option.IsActive && option.IsDefault)?.OptionCode
            ?? throw new InvalidOperationException("A default active account lifecycle stage must be configured before intake promotion.");
        return await _accountRepository.CreateAsync(new CreateAccountRequest
        {
            TenantId = tenantId,
            AccountName = ResolveAccountName(intake, stamp),
            AccountTypeCode = accountType.TypeCode,
            MainEmail = string.IsNullOrWhiteSpace(intake.Email) ? null : intake.Email,
            MainPhone = string.IsNullOrWhiteSpace(intake.Phone) ? null : intake.Phone,
            StatusCode = status,
            LifecycleStageCode = lifecycleStage,
            AnnualRevenue = intake.EstimatedPremium > 0 ? intake.EstimatedPremium : null,
            OwnerUserId = intake.AssignedToUserId,
            CreatedByUserId = userId
        }, cancellationToken);
    }

    private static AccountMatchCriteria BuildCriteria(SubmissionIntakeDto intake) => new()
    {
        TenantId = intake.TenantId,
        BusinessName = intake.BusinessName,
        Fein = intake.Fein,
        Email = intake.Email,
        Phone = intake.Phone,
        AddressLine = intake.AddressLine,
        PostalCode = intake.PostalCode,
        ExistingPolicyNumber = intake.ExistingPolicyNumber,
        ProducerCode = intake.ProducerCode
    };

    private static string ResolveAccountName(SubmissionIntakeDto intake, string stamp)
    {
        if (!string.IsNullOrWhiteSpace(intake.BusinessName)) return intake.BusinessName.Trim();
        if (!string.IsNullOrWhiteSpace(intake.ApplicantName)) return intake.ApplicantName.Trim();
        return $"Prospect {stamp}";
    }

    private async Task<string> ResolveDefaultForecastCategoryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var categories = await _forecastCategoryService.SearchAsync(tenantId, null, 1, 200, cancellationToken);
        var category = categories.Items
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.CategoryName)
            .FirstOrDefault();

        if (category is null)
        {
            throw new InvalidOperationException("Submission intake cannot create an opportunity because no DB-backed forecast category is configured.");
        }

        return category.CategoryName;
    }
}
