using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Activity read surface (audit / usage / logins).
//
// A thin, tenant-scoped read layer over the existing audit-event and usage
// ledger tables plus the login-history table. Windows are expressed in whole
// days back from "now" and clamped to sane bounds so the admin UI can never ask
// for an unbounded scan. RecordLoginAsync is best-effort: authentication must
// never fail because a history row could not be written, so any error is logged
// and swallowed.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ActivityService(ISaasRepository repository, ILogger<ActivityService> logger) : IActivityService
{
    private const int MaxDays = 365;
    private const int MaxTake = 500;

    public Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(Guid tenantId, int days, int take, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        return repository.ListAuditEventsAsync(tenantId, SinceUtc(days), ClampTake(take), ct);
    }

    public Task<IReadOnlyList<AuditEventDto>> ListAuditEventsForUserAsync(Guid tenantId, Guid userId, int days, int take, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        EnsureUser(userId);
        return repository.ListAuditEventsForUserAsync(tenantId, userId, SinceUtc(days), ClampTake(take), ct);
    }

    public Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageAsync(Guid tenantId, int days, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        return repository.SummarizeUsageAsync(tenantId, SinceUtc(days), ct);
    }

    public Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageForUserAsync(Guid tenantId, Guid userId, int days, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        EnsureUser(userId);
        return repository.SummarizeUsageForUserAsync(tenantId, userId, SinceUtc(days), ct);
    }

    public Task<IReadOnlyList<LoginHistoryDto>> ListLoginHistoryAsync(Guid tenantId, int days, int take, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        return repository.ListLoginHistoryAsync(tenantId, SinceUtc(days), ClampTake(take), ct);
    }

    public async Task RecordLoginAsync(RecordLoginRequest request, CancellationToken ct = default)
    {
        try
        {
            await repository.RecordLoginAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Never let history writes affect the authentication outcome.
            logger.LogWarning(ex, "Failed to record login history for {Email} ({Outcome}).", request.Email, request.OutcomeCode);
        }
    }

    private static DateTime SinceUtc(int days) => DateTime.UtcNow.AddDays(-Math.Clamp(days <= 0 ? 30 : days, 1, MaxDays));

    private static int ClampTake(int take) => Math.Clamp(take <= 0 ? 100 : take, 1, MaxTake);

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new UserManagementForbiddenException("A tenant scope is required.");
    }

    private static void EnsureUser(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new UserManagementForbiddenException("A user scope is required.");
    }
}
