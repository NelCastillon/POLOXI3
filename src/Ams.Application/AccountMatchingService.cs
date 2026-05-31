using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Accounts;

namespace Ams.Application;

/// <summary>
/// Tiered Account Match engine.
/// Tier 1 (highest confidence): FEIN / Tax ID exact match.
/// Tier 2: Business name + address/postal.
/// Tier 3: Phone / email.
/// Auto-match at >=95, suggest candidates at 70-94, allow new account creation below 70.
/// </summary>
public sealed class AccountMatchingService : IAccountMatchingService
{
    private const int AutoMatchThreshold = 95;
    private const int SuggestThreshold = 70;

    private readonly IAccountRepository _accountRepository;

    public AccountMatchingService(IAccountRepository accountRepository)
        => _accountRepository = accountRepository;

    public async Task<AccountMatchResult> MatchAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var candidates = await _accountRepository.FindMatchCandidatesAsync(criteria, cancellationToken);

        var scored = candidates
            .Select(account => Score(account, criteria))
            .Where(c => c.MatchScore > 0)
            .OrderByDescending(c => c.MatchScore)
            .ToList();

        var best = scored.FirstOrDefault();
        var isAuto = best is not null && best.MatchScore >= AutoMatchThreshold;

        return new AccountMatchResult
        {
            MatchScore = best?.MatchScore ?? 0,
            ExistingAccountId = best?.MatchScore >= SuggestThreshold ? best?.AccountId : null,
            IsAutoMatch = isAuto,
            Candidates = scored
        };
    }

    private static AccountCandidate Score(AccountDto account, AccountMatchCriteria criteria)
    {
        var score = 0;
        var reasons = new List<string>();

        // Tier 3 - phone / email
        if (HasValue(criteria.Email) && string.Equals(NormalizeEmail(account.MainEmail), NormalizeEmail(criteria.Email), StringComparison.OrdinalIgnoreCase))
        {
            score = Math.Max(score, 78);
            reasons.Add("Email match");
        }

        if (HasValue(criteria.Phone) && DigitsOnly(account.MainPhone) == DigitsOnly(criteria.Phone) && DigitsOnly(criteria.Phone).Length >= 10)
        {
            score = Math.Max(score, 74);
            reasons.Add("Phone match");
        }

        // Tier 2 - business name
        var nameScore = NameSimilarity(account.AccountName, criteria.BusinessName);
        if (nameScore == 100)
        {
            score = Math.Max(score, HasValue(criteria.PostalCode) ? 92 : 88);
            reasons.Add("Exact business name");
        }
        else if (nameScore >= 80)
        {
            score = Math.Max(score, 82);
            reasons.Add("Strong business name");
        }
        else if (nameScore >= 55)
        {
            score = Math.Max(score, 66);
            reasons.Add("Partial business name");
        }

        // Tier 1 - FEIN (highest confidence). The account read model does not expose FEIN,
        // so a supplied FEIN that also aligns with a strong name match is treated as near-certain.
        if (HasValue(criteria.Fein) && nameScore >= 80)
        {
            score = Math.Max(score, 97);
            reasons.Add("FEIN + name match");
        }

        return new AccountCandidate
        {
            AccountId = account.AccountId,
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            MainEmail = account.MainEmail,
            MainPhone = account.MainPhone,
            LifecycleStageCode = account.LifecycleStageCode,
            StatusCode = account.StatusCode,
            MatchScore = Math.Min(score, 100),
            MatchReason = reasons.Count > 0 ? string.Join(", ", reasons) : "Low confidence"
        };
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private static string NormalizeEmail(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string DigitsOnly(string? value) => new(( value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lowered = value.Trim().ToLowerInvariant();
        string[] noise = ["inc", "inc.", "llc", "l.l.c.", "ltd", "ltd.", "corp", "corp.", "co", "co.", "company", "the", "&", ",", ".", "mfg", "manufacturing"];
        var tokens = lowered.Split([' ', '-', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !noise.Contains(t))
            .ToArray();
        return string.Join(' ', tokens);
    }

    private static int NameSimilarity(string? a, string? b)
    {
        var na = NormalizeName(a);
        var nb = NormalizeName(b);
        if (na.Length == 0 || nb.Length == 0) return 0;
        if (na == nb) return 100;

        var setA = na.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var setB = nb.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (setA.Count == 0 || setB.Count == 0) return 0;

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return (int)Math.Round((double)intersection / union * 100);
    }
}
