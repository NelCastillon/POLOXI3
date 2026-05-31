namespace Ams.Application.Features.Accounts;

/// <summary>
/// Criteria used by the Account Match engine to find existing accounts before
/// creating a new one during direct submission intake normalization.
/// </summary>
public sealed class AccountMatchCriteria
{
    public Guid TenantId { get; set; }
    public string? BusinessName { get; set; }
    public string? Fein { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? ExistingPolicyNumber { get; set; }
    public string? ProducerCode { get; set; }
}

public sealed class AccountCandidate
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? MainEmail { get; set; }
    public string? MainPhone { get; set; }
    public string? LifecycleStageCode { get; set; }
    public string? StatusCode { get; set; }
    public int MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
}

/// <summary>
/// Result of running the Account Match engine. Auto-match (>=95) means a single
/// high-confidence account; suggestions (70-94) require producer choice; below 70
/// allows new account creation.
/// </summary>
public sealed class AccountMatchResult
{
    public int MatchScore { get; set; }
    public Guid? ExistingAccountId { get; set; }
    public bool IsAutoMatch { get; set; }
    public IReadOnlyList<AccountCandidate> Candidates { get; set; } = [];
}
