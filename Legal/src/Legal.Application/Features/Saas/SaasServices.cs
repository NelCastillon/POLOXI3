using System.Security.Cryptography;
using System.Text;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — application service implementations.
//
// Provider-agnostic services over ISaasRepository. All operational values are
// DB-backed; only security/algorithm constants (code length, expiry, hashing)
// live in code. Emails are sent through IJudzEmailSender.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>6-digit, single-use, time-limited verification codes (hashed at rest).</summary>
public sealed class EmailVerificationService(ISaasRepository repository, IJudzEmailSender emailSender) : IEmailVerificationService
{
    private const int CodeDigits = 6;
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;
    private const string Purpose = "EmailVerification";

    public async Task IssueCodeAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var code = GenerateCode();
        await repository.CreateVerificationChallengeAsync(userId, Purpose, HashCode(code), DateTime.UtcNow.Add(Expiry), MaxAttempts, ct);
        await emailSender.SendVerificationCodeAsync(email, code, ct);
    }

    public async Task<bool> ResendCodeAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var existing = await repository.GetActiveChallengeAsync(userId, Purpose, ct);
        if (existing is not null && existing.ConsumedAtUtc is null && existing.ExpiresAtUtc > DateTime.UtcNow)
        {
            // Consume the old challenge and issue a fresh one to keep codes single-use.
            await repository.ConsumeChallengeAsync(existing.ChallengeId, ct);
        }

        await IssueCodeAsync(userId, email, ct);
        return true;
    }

    public async Task<EmailVerificationOutcome> VerifyCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var challenge = await repository.GetActiveChallengeAsync(userId, Purpose, ct);
        if (challenge is null || challenge.ConsumedAtUtc is not null)
            return EmailVerificationOutcome.NoActiveChallenge;

        if (challenge.ExpiresAtUtc <= DateTime.UtcNow)
            return EmailVerificationOutcome.Expired;

        if (challenge.AttemptCount >= challenge.MaxAttempts)
            return EmailVerificationOutcome.TooManyAttempts;

        var submitted = HashCode(code);
        if (!CryptographicOperations.FixedTimeEquals(submitted, challenge.CodeHash))
        {
            await repository.IncrementChallengeAttemptAsync(challenge.ChallengeId, ct);
            return EmailVerificationOutcome.InvalidCode;
        }

        await repository.ConsumeChallengeAsync(challenge.ChallengeId, ct);
        return EmailVerificationOutcome.Success;
    }

    private static string GenerateCode()
    {
        var max = (int)Math.Pow(10, CodeDigits);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{CodeDigits}");
    }

    private static byte[] HashCode(string code)
        => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}

/// <summary>Idempotent workspace provisioning: tenant → owner membership → subscription → placement → audit.</summary>
public sealed class WorkspaceProvisioningService(ISaasRepository repository) : IWorkspaceProvisioningService
{
    private const string OwnerRoleCode = "OWNER";
    private const string EarlyAccessPlanCode = "EARLY_ACCESS";

    public async Task<Guid> ProvisionAsync(Guid userId, string email, string? displayName, CancellationToken ct = default)
    {
        var status = await repository.GetProvisioningStatusAsync(userId, ct);
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await repository.GetPrimaryMembershipAsync(userId, ct);
            if (existing is not null)
                return existing.TenantId;
        }

        await repository.UpsertProvisioningStatusAsync(userId, "InProgress", null, null, ct);

        try
        {
            var name = string.IsNullOrWhiteSpace(displayName) ? DeriveName(email) : displayName!;
            var slug = await BuildUniqueSlugAsync(name, ct);
            var tenantId = await repository.CreateTenantAsync(name, slug, userId, ct);

            var ownerRoleId = await repository.GetRoleIdByCodeAsync(OwnerRoleCode, ct)
                ?? throw new InvalidOperationException("OWNER role is not seeded.");
            await repository.CreateMembershipAsync(tenantId, userId, ownerRoleId, ct);

            var planId = await repository.GetPlanIdByCodeAsync(EarlyAccessPlanCode, ct)
                ?? throw new InvalidOperationException("EARLY_ACCESS plan is not seeded.");
            await repository.CreateSubscriptionAsync(tenantId, planId, ct);

            await repository.CreateTenantPlacementAsync(tenantId, "default", ct);
            await repository.WriteAuditAsync(tenantId, userId, "TENANT_CREATED", null, "Tenant", tenantId, null, null, ct);
            await repository.WriteAuditAsync(tenantId, userId, "MEMBERSHIP_CREATED", null, "TenantMembership", null, null, null, ct);

            await repository.UpsertProvisioningStatusAsync(userId, "Completed", tenantId, null, ct);
            return tenantId;
        }
        catch (Exception ex)
        {
            await repository.UpsertProvisioningStatusAsync(userId, "Failed", null, ex.Message, ct);
            throw;
        }
    }

    private async Task<string> BuildUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        // Slug uniqueness is enforced by a filtered unique index; add a short suffix to avoid collisions.
        var suffix = Guid.NewGuid().ToString("N")[..6];
        await Task.CompletedTask;
        return $"{baseSlug}-{suffix}";
    }

    private static string DeriveName(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }

    private static string Slugify(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_' or '.') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "workspace" : slug;
    }
}

/// <summary>Resolves the authoritative active tenant + permissions.</summary>
public sealed class TenantContextService(ISaasRepository repository) : ITenantContextService
{
    public Task<TenantMembershipDto?> ResolveMembershipAsync(Guid userId, CancellationToken ct = default)
        => repository.GetPrimaryMembershipAsync(userId, ct);

    public Task<IReadOnlyList<string>> ResolvePermissionsAsync(Guid roleId, CancellationToken ct = default)
        => repository.GetPermissionsAsync(roleId, ct);

    public Task<IReadOnlyList<string>> ResolveEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => repository.GetEffectivePermissionsAsync(userId, tenantId, ct);
}

/// <summary>Resolves effective entitlements for a tenant (plan grants + overrides).</summary>
public sealed class EntitlementService(ISaasRepository repository) : IEntitlementService
{
    public Task<IReadOnlyList<EntitlementResolution>> GetEntitlementsAsync(Guid tenantId, CancellationToken ct = default)
        => repository.ResolveEntitlementsAsync(tenantId, ct);

    public async Task<EntitlementResolution?> GetEntitlementAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        var all = await repository.ResolveEntitlementsAsync(tenantId, ct);
        return all.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Authorization: authenticated → membership → permission → entitlement → matter access.</summary>
public sealed class CapabilityAuthorizationService(ISaasRepository repository, IEntitlementService entitlements) : ICapabilityAuthorizationService
{
    public async Task<CapabilityAuthorizationResult> AuthorizeAsync(
        Guid userId, Guid tenantId, string capabilityCode, Guid? matterId,
        IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return CapabilityAuthorizationResult.Deny("An authenticated user and tenant are required.");

        var capability = await repository.GetCapabilityAsync(capabilityCode, ct);
        if (capability is null)
            return CapabilityAuthorizationResult.Deny($"Unknown capability '{capabilityCode}'.");

        if (!string.IsNullOrWhiteSpace(capability.Permission)
            && !permissions.Contains("NAV_ALL", StringComparer.OrdinalIgnoreCase)
            && !permissions.Contains(capability.Permission, StringComparer.OrdinalIgnoreCase))
            return CapabilityAuthorizationResult.Deny($"Missing permission '{capability.Permission}'.");

        var entitlement = await entitlements.GetEntitlementAsync(tenantId, capability.EntitlementCode, ct);
        if (entitlement is null || !entitlement.IsEnabled)
            return CapabilityAuthorizationResult.Deny($"Plan does not include '{capability.EntitlementCode}'.");

        if (capability.RequiresMatter && (matterId is null || matterId == Guid.Empty))
            return CapabilityAuthorizationResult.Deny("This capability requires a matter.");

        return CapabilityAuthorizationResult.Allow(capability);
    }
}

/// <summary>Usage capacity check + atomic reservation (customer meters).</summary>
public sealed class UsageService(ISaasRepository repository) : IUsageService
{
    private static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(15);

    public async Task<UsageCheckResult> CheckAsync(Guid tenantId, string meterCode, string limitEntitlementCode, CancellationToken ct = default)
    {
        var used = await repository.GetMonthlyUsageAsync(tenantId, meterCode, ct);
        var entitlements = await repository.ResolveEntitlementsAsync(tenantId, ct);
        var limitEntitlement = entitlements.FirstOrDefault(e => string.Equals(e.Code, limitEntitlementCode, StringComparison.OrdinalIgnoreCase));
        var limit = limitEntitlement?.LimitValue ?? long.MaxValue;
        return new UsageCheckResult(used < limit, limit, used, meterCode);
    }

    public Task<Guid> ReserveAsync(Guid tenantId, Guid? executionId, string meterCode, CancellationToken ct = default)
        => repository.ReserveUsageAsync(tenantId, executionId, meterCode, 1, DateTime.UtcNow.Add(ReservationTtl), ct);

    public async Task CommitAsync(Guid reservationId, Guid tenantId, Guid? userId, Guid? executionId, Guid? matterId, string meterCode, string? correlationId, CancellationToken ct = default)
    {
        await repository.UpdateReservationStatusAsync(reservationId, "Committed", ct);
        await repository.RecordUsageAsync(tenantId, userId, executionId, matterId, meterCode, "Customer", 1, correlationId, ct);
    }

    public Task ReleaseAsync(Guid reservationId, CancellationToken ct = default)
        => repository.UpdateReservationStatusAsync(reservationId, "Released", ct);
}

/// <summary>The single execution boundary: authorize → idempotency → reserve → create execution → audit.</summary>
public sealed class IntelligenceExecutionService(
    ISaasRepository repository,
    ICapabilityAuthorizationService authorization,
    IUsageService usage) : IIntelligenceExecutionService
{
    private const string Operation = "IntelligenceExecution";

    public async Task<IntelligenceExecutionDto> CreateAsync(
        Guid userId, Guid tenantId, IReadOnlyCollection<string> permissions,
        ExecuteCapabilityRequest request, CancellationToken ct = default)
    {
        var auth = await authorization.AuthorizeAsync(userId, tenantId, request.CapabilityCode, request.MatterId, permissions, ct);
        if (!auth.Allowed || auth.Capability is null)
            throw new CapabilityAuthorizationException(auth.DenialReason ?? "Not authorized.");

        var capability = auth.Capability;

        // Idempotency: a repeated key returns the original execution instead of creating a new one.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingId = await repository.GetIdempotentExecutionAsync(tenantId, Operation, request.IdempotencyKey!, ct);
            if (existingId is not null)
            {
                var existing = await repository.GetExecutionAsync(tenantId, existingId.Value, ct);
                if (existing is not null)
                    return existing;
            }
        }

        // Quota check for metered capabilities.
        Guid? reservationId = null;
        if (capability.IsMetered && !string.IsNullOrWhiteSpace(capability.CustomerMeterCode))
        {
            var limitCode = MonthlyLimitCodeFor(capability.EntitlementCode);
            var check = await usage.CheckAsync(tenantId, capability.CustomerMeterCode!, limitCode, ct);
            if (!check.HasCapacity)
                throw new UsageQuotaExceededException(capability.CustomerMeterCode!, check.Limit);

            reservationId = await usage.ReserveAsync(tenantId, null, capability.CustomerMeterCode!, ct);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var executionId = await repository.CreateExecutionAsync(tenantId, userId, request.MatterId, capability.Code, correlationId, request.ParentExecutionId, ct);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            await repository.CreateIdempotencyRecordAsync(tenantId, Operation, request.IdempotencyKey!, executionId, DateTime.UtcNow.AddHours(24), ct);

        // Commit customer usage now that the execution is created (domain dispatch happens later).
        if (reservationId is not null && !string.IsNullOrWhiteSpace(capability.CustomerMeterCode))
            await usage.CommitAsync(reservationId.Value, tenantId, userId, executionId, request.MatterId, capability.CustomerMeterCode!, correlationId, ct);

        await repository.UpdateExecutionStatusAsync(executionId, "Queued", null, ct);
        await repository.WriteAuditAsync(tenantId, userId, AuditTypeFor(capability.Code), executionId, "IntelligenceExecution", executionId, null, correlationId, ct);

        return await repository.GetExecutionAsync(tenantId, executionId, ct)
            ?? throw new InvalidOperationException("Execution was not persisted.");
    }

    public Task<IntelligenceExecutionDto?> GetAsync(Guid tenantId, Guid executionId, CancellationToken ct = default)
        => repository.GetExecutionAsync(tenantId, executionId, ct);

    // Map a feature entitlement (e.g. research.search) to its monthly limit entitlement (monthly.search).
    private static string MonthlyLimitCodeFor(string entitlementCode) => entitlementCode switch
    {
        "research.search" => "monthly.search",
        "research.poloxi" => "monthly.research",
        "legal.search" => "monthly.legal_search",
        "legal.research" => "monthly.legal_search",
        "legal.decision" => "monthly.legal_decision",
        "math.formalization" => "monthly.math",
        "math.solver" => "monthly.math",
        _ => "monthly.search"
    };

    private static string AuditTypeFor(string capabilityCode) => capabilityCode switch
    {
        "research.search" => "SEARCH_EXECUTED",
        "research.poloxi" => "RESEARCH_EXECUTED",
        "legal.search" => "LEGAL_SEARCH_EXECUTED",
        "legal.decision" => "DECISION_EXECUTED",
        "math.formalization" => "MATH_EXECUTED",
        "math.solver" => "MATH_EXECUTED",
        _ => "EXECUTION_CREATED"
    };
}
