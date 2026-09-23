using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Tenant invitation lifecycle (Phase B).
//
// Create: generates a cryptographically-random single-use token, stores only its
// SHA-256 hash, persists pending role assignments, then enqueues the invitation
// email through the transactional outbox (sent after commit, retry-safe).
//
// Accept: resolves the invitation by raw token hash, validates lifecycle/expiry,
// provisions the identity (existing user → membership; new user → create account
// + confirm email + membership), applies the pending roles' primary role, and
// consumes the invitation idempotently. All values (statuses/roles) are DB-driven.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class InvitationService(
    ISaasRepository repository,
    IUserAccountCreator accountCreator,
    IConfiguration configuration) : IInvitationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public const string InvitationEmailMessageType = "InvitationEmail";

    public Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default)
        => repository.ListInvitationsAsync(tenantId, ct);

    public async Task<TenantInvitationDto> CreateInvitationAsync(Guid tenantId, Guid? actorUserId, CreateInvitationRequest request, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new UserManagementForbiddenException("A tenant scope is required.");

        var email = NormalizeEmail(request.Email);
        var normalizedEmail = email.ToUpperInvariant();

        var roleId = await ResolveRoleIdAsync(request.RoleCode, ct);
        var additionalRoleIds = await ResolveRoleIdsAsync(request.AdditionalRoleCodes, ct);

        var (rawToken, tokenHash) = GenerateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(InvitationLifetime);

        var invitationId = await repository.CreateInvitationAsync(
            tenantId, normalizedEmail, roleId, tokenHash, expiresAtUtc, actorUserId, additionalRoleIds, ct);

        var groupIds = request.GroupIds ?? [];
        if (groupIds.Count > 0)
            await repository.AddInvitationGroupsAsync(invitationId, groupIds, actorUserId, ct);

        await QueueInvitationEmailAsync(tenantId, email, roleId, rawToken, ct);

        return await repository.GetInvitationAsync(invitationId, ct)
            ?? throw new InvalidOperationException("Invitation was created but could not be loaded.");
    }

    public async Task ResendInvitationAsync(Guid tenantId, Guid? actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await repository.GetInvitationAsync(invitationId, ct)
            ?? throw new UserManagementForbiddenException("Invitation not found.");
        if (invitation.TenantId != tenantId)
            throw new UserManagementForbiddenException("You may only manage invitations within your own tenant.");
        if (!string.Equals(invitation.StatusCode, InvitationStatus.Pending, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(invitation.StatusCode, InvitationStatus.Expired, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only pending or expired invitations can be resent (current status '{invitation.StatusCode}').");

        var (rawToken, tokenHash) = GenerateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(InvitationLifetime);
        await repository.UpdateInvitationTokenAsync(invitationId, tokenHash, expiresAtUtc, actorUserId, ct);

        await QueueInvitationEmailAsync(tenantId, invitation.Email, invitation.RoleId, rawToken, ct);
    }

    public async Task RevokeInvitationAsync(Guid tenantId, Guid? actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await repository.GetInvitationAsync(invitationId, ct)
            ?? throw new UserManagementForbiddenException("Invitation not found.");
        if (invitation.TenantId != tenantId)
            throw new UserManagementForbiddenException("You may only manage invitations within your own tenant.");

        await repository.RevokeInvitationAsync(invitationId, actorUserId, ct);
    }

    public async Task<InvitationLookupDto?> LookupByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var row = await repository.GetInvitationByTokenHashAsync(HashToken(token), ct);
        if (row is null)
            return null;

        var tenantName = await repository.GetTenantNameAsync(row.TenantId, ct) ?? "your workspace";
        var invitation = await repository.GetInvitationAsync(row.InvitationId, ct);
        var roleName = invitation?.RoleName ?? "Member";

        var status = ResolveEffectiveStatus(row);
        var existingUserId = await repository.FindUserIdByEmailAsync(row.EmailNormalized, ct);

        return new InvitationLookupDto(
            row.InvitationId,
            row.TenantId,
            tenantName,
            row.EmailNormalized,
            roleName,
            status,
            row.ExpiresAtUtc,
            RequiresAccountCreation: existingUserId is null);
    }

    public async Task<AcceptInvitationResult> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return new AcceptInvitationResult(InvitationAcceptOutcome.InvalidToken, null, null, null, "Missing invitation token.");

        var row = await repository.GetInvitationByTokenHashAsync(HashToken(request.Token), ct);
        if (row is null)
            return new AcceptInvitationResult(InvitationAcceptOutcome.InvalidToken, null, null, null, "This invitation link is invalid.");

        if (string.Equals(row.StatusCode, InvitationStatus.Revoked, StringComparison.OrdinalIgnoreCase))
            return new AcceptInvitationResult(InvitationAcceptOutcome.Revoked, null, null, null, "This invitation has been revoked.");
        if (string.Equals(row.StatusCode, InvitationStatus.Accepted, StringComparison.OrdinalIgnoreCase))
            return new AcceptInvitationResult(InvitationAcceptOutcome.AlreadyAccepted, null, null, null, "This invitation has already been accepted.");
        if (!string.Equals(row.StatusCode, InvitationStatus.Pending, StringComparison.OrdinalIgnoreCase) || row.ExpiresAtUtc <= DateTime.UtcNow)
            return new AcceptInvitationResult(InvitationAcceptOutcome.Expired, null, null, null, "This invitation has expired.");

        var email = row.EmailNormalized;
        var existingUserId = await repository.FindUserIdByEmailAsync(email, ct);

        Guid userId;
        if (existingUserId is Guid existing)
        {
            userId = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                return new AcceptInvitationResult(InvitationAcceptOutcome.AccountDetailsRequired, row.TenantId, null, null,
                    "Please provide your name and a password to create your account.");

            userId = await accountCreator.CreateAsync(email, request.FirstName!.Trim(), request.LastName!.Trim(),
                request.Password!, emailConfirmed: true, ct);
        }

        // Apply the invitation's primary role (additional roles are captured for Phase C group/role expansion).
        var roleIds = await repository.GetInvitationRoleIdsAsync(row.InvitationId, ct);
        var primaryRoleId = roleIds.Count > 0 ? roleIds[0] : row.RoleId;

        var membershipId = await repository.UpsertMembershipAsync(row.TenantId, userId, primaryRoleId, userId, ct);

        // Apply the invitation's pending group assignments (each group carries its own roles).
        var groupIds = await repository.GetInvitationGroupIdsAsync(row.InvitationId, ct);
        foreach (var groupId in groupIds)
            await repository.AddGroupMemberAsync(row.TenantId, groupId, userId, userId, ct);

        await repository.MarkInvitationAcceptedAsync(row.InvitationId, userId, ct);

        return new AcceptInvitationResult(InvitationAcceptOutcome.Success, row.TenantId, membershipId, userId, "Invitation accepted.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task QueueInvitationEmailAsync(Guid tenantId, string email, Guid roleId, string rawToken, CancellationToken ct)
    {
        var tenantName = await repository.GetTenantNameAsync(tenantId, ct) ?? "your workspace";
        var acceptUrl = BuildAcceptUrl(rawToken);

        // Resolve a human role name for the email body (best-effort).
        var roleName = "Member";
        var tenantInvitations = await repository.ListInvitationsAsync(tenantId, ct);
        var match = tenantInvitations.FirstOrDefault(i => i.RoleId == roleId);
        if (match is not null)
            roleName = match.RoleName;

        var payload = new InvitationEmailPayload(email, tenantName, roleName, acceptUrl);
        await repository.EnqueueOutboxAsync(InvitationEmailMessageType, JsonSerializer.Serialize(payload), ct);
    }

    private string BuildAcceptUrl(string rawToken)
    {
        var baseUrl = configuration["Web:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://app.judz.ai/";
        return $"{baseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(rawToken)}";
    }

    private async Task<Guid> ResolveRoleIdAsync(string roleCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            throw new InvalidOperationException("A role is required.");
        return await repository.GetRoleIdByCodeAsync(roleCode, ct)
            ?? throw new InvalidOperationException($"Role '{roleCode}' is not defined.");
    }

    private async Task<IReadOnlyList<Guid>> ResolveRoleIdsAsync(IReadOnlyList<string>? roleCodes, CancellationToken ct)
    {
        if (roleCodes is null || roleCodes.Count == 0)
            return [];
        var ids = new List<Guid>();
        foreach (var code in roleCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var id = await repository.GetRoleIdByCodeAsync(code, ct);
            if (id is Guid g)
                ids.Add(g);
        }
        return ids;
    }

    private static string ResolveEffectiveStatus(InvitationRow row)
    {
        if (string.Equals(row.StatusCode, InvitationStatus.Pending, StringComparison.OrdinalIgnoreCase) && row.ExpiresAtUtc <= DateTime.UtcNow)
            return InvitationStatus.Expired;
        return row.StatusCode;
    }

    private static (string RawToken, byte[] Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(bytes).ToLowerInvariant();
        return (rawToken, HashToken(rawToken));
    }

    private static byte[] HashToken(string rawToken)
        => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("An email address is required.");
        return email.Trim();
    }
}
