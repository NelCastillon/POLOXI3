using Ams.Application.Features.Accounts;

namespace Ams.Application.Abstractions.Services;

/// <summary>
/// Account Match / Duplicate Detection engine. Runs before any new Account is created
/// during direct submission intake so the system avoids duplicate customer records.
/// </summary>
public interface IAccountMatchingService
{
    Task<AccountMatchResult> MatchAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default);
}
