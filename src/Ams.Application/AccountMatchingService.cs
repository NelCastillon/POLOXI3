using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.SearchMatching;

namespace Ams.Application;

public sealed class AccountMatchingService(IEntityMatchingService matchingService) : IAccountMatchingService
{
    public async Task<AccountMatchResult> MatchAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string?>
        {
            ["BusinessName"] = criteria.BusinessName,
            ["Fein"] = criteria.Fein,
            ["EmailDomain"] = EmailDomain(criteria.Email),
            ["Phone"] = criteria.Phone,
            ["Address"] = string.Join(' ', new[] { criteria.AddressLine, criteria.PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value))),
            ["PolicyNumber"] = criteria.ExistingPolicyNumber,
            ["ProducerCode"] = criteria.ProducerCode
        };
        var result = await matchingService.FindMatchesAsync(new EntityMatchRequest
        {
            TenantId = criteria.TenantId,
            ProfileCode = "ACCOUNT_DUPLICATE",
            EntityTypeCode = "Account",
            CorrelationId = $"account-match:{Guid.NewGuid():N}",
            Fields = fields
        }, cancellationToken);
        var candidates = result.Candidates.Select(candidate => new AccountCandidate
        {
            AccountId = candidate.EntityId,
            AccountNumber = candidate.SecondaryText ?? string.Empty,
            AccountName = candidate.DisplayName,
            MatchScore = Convert.ToInt32(decimal.Round(candidate.OverallScore, 0)),
            MatchReason = string.Join(", ", candidate.Reasons.Where(reason => reason.WeightedScore > 0 || reason.IsDiscrepancy).Select(reason => reason.Explanation))
        }).ToList();
        var best = candidates.FirstOrDefault();
        return new AccountMatchResult
        {
            MatchScore = best?.MatchScore ?? 0,
            ExistingAccountId = best?.AccountId,
            IsAutoMatch = false,
            Candidates = candidates
        };
    }

    private static string? EmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var separator = email.LastIndexOf('@');
        return separator >= 0 && separator < email.Length - 1 ? email[(separator + 1)..].Trim() : null;
    }
}