using System.Security.Cryptography;
using System.Text;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Legal.Application.Tests.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// InvitationService — Phase B lifecycle tests.
//
// Covers the security- and correctness-critical paths of the tenant invitation
// flow with an in-memory fake repository (no DB): token hashing (only hashes are
// stored), idempotent/duplicate-safe acceptance, expiry, revocation, existing-vs-
// new user provisioning, pending-role → membership conversion, and outbox enqueue.
//
// The service never surfaces the raw token (it is generated internally and only
// its SHA-256 hash is persisted), so acceptance/lookup tests seed the fake repo
// directly with a known raw token whose hash we compute the same way the service
// does (SHA256 over UTF-8 bytes of the lowercase hex token).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class InvitationServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string RawToken = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

    private static byte[] HashToken(string rawToken) => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    private static InvitationService CreateService(FakeSaasRepository repo, FakeAccountCreator? creator = null)
        => new(repo, creator ?? new FakeAccountCreator(), new StubConfiguration());

    // ── Create ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateInvitation_StoresPendingHashedInvitationAndEnqueuesEmail()
    {
        var repo = new FakeSaasRepository();
        var service = CreateService(repo);

        var dto = await service.CreateInvitationAsync(TenantId, actorUserId: null,
            new CreateInvitationRequest(TenantId, "New.User@Example.com", "MEMBER"));

        var stored = Assert.Single(repo.Invitations.Values);
        Assert.Equal(InvitationStatus.Pending, stored.StatusCode);
        Assert.Equal("NEW.USER@EXAMPLE.COM", stored.EmailNormalized);
        Assert.Equal(MemberRoleId, stored.RoleId);
        Assert.NotEmpty(stored.TokenHash); // hash persisted, never plaintext
        Assert.Equal(dto.InvitationId, stored.InvitationId);
        var outbox = Assert.Single(repo.Outbox);
        Assert.Equal(InvitationService.InvitationEmailMessageType, outbox.MessageType);
    }

    [Fact]
    public async Task CreateInvitation_WithGroupIds_PersistsPendingGroups()
    {
        var repo = new FakeSaasRepository();
        var service = CreateService(repo);
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        var dto = await service.CreateInvitationAsync(TenantId, actorUserId: null,
            new CreateInvitationRequest(TenantId, "New.User@Example.com", "MEMBER", GroupIds: [groupA, groupB]));

        Assert.True(repo.InvitationGroups.TryGetValue(dto.InvitationId, out var stored));
        Assert.Equal(new[] { groupA, groupB }, stored);
    }

    [Fact]
    public async Task CreateInvitation_WithoutTenantScope_IsForbidden()
    {
        var service = CreateService(new FakeSaasRepository());

        await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.CreateInvitationAsync(Guid.Empty, null, new CreateInvitationRequest(Guid.Empty, "x@y.com", "MEMBER")));
    }

    // ── Lookup ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task LookupByToken_ReturnsNonSensitiveView_ForPendingInvitation()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);

        var lookup = await service.LookupByTokenAsync(RawToken);

        Assert.NotNull(lookup);
        Assert.Equal(TenantId, lookup!.TenantId);
        Assert.Equal(InvitationStatus.Pending, lookup.StatusCode);
        Assert.True(lookup.RequiresAccountCreation); // no existing user for this email
    }

    [Fact]
    public async Task LookupByToken_ReturnsExpired_WhenPastExpiry()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(repo);

        var lookup = await service.LookupByTokenAsync(RawToken);

        Assert.Equal(InvitationStatus.Expired, lookup!.StatusCode);
    }

    [Fact]
    public async Task LookupByToken_UnknownToken_ReturnsNull()
    {
        var service = CreateService(new FakeSaasRepository());

        Assert.Null(await service.LookupByTokenAsync("does-not-exist"));
    }

    // ── Accept ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Accept_NewUser_CreatesAccountAndMembershipAndConsumesInvitation()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var creator = new FakeAccountCreator();
        var service = CreateService(repo, creator);

        var result = await service.AcceptInvitationAsync(
            new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        Assert.Equal(InvitationAcceptOutcome.Success, result.Outcome);
        Assert.Equal(creator.CreatedUserId, result.UserId);
        var membership = Assert.Single(repo.Memberships);
        Assert.Equal(MemberRoleId, membership.RoleId);
        Assert.Equal(InvitationStatus.Accepted, repo.Invitations.Values.Single().StatusCode);
    }

    [Fact]
    public async Task Accept_AppliesPendingGroupMemberships()
    {
        var repo = new FakeSaasRepository();
        var invitationId = repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        repo.InvitationGroups[invitationId] = [groupA, groupB];
        var service = CreateService(repo, new FakeAccountCreator());

        var result = await service.AcceptInvitationAsync(
            new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        Assert.Equal(InvitationAcceptOutcome.Success, result.Outcome);
        Assert.Equal(2, repo.GroupMembers.Count);
        Assert.Contains(repo.GroupMembers, m => m.GroupId == groupA && m.UserId == result.UserId);
        Assert.Contains(repo.GroupMembers, m => m.GroupId == groupB && m.UserId == result.UserId);
    }

    [Fact]
    public async Task ResolveEffectivePermissions_UsesUserTenantScopedAdditivePath()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeSaasRepository
        {
            EffectivePermissions = ["matters.read", "matters.write", "billing.read"]
        };
        var service = new TenantContextService(repo);

        var permissions = await service.ResolveEffectivePermissionsAsync(userId, TenantId);

        Assert.Equal(userId, repo.CapturedEffectiveUserId);
        Assert.Equal(TenantId, repo.CapturedEffectiveTenantId);
        Assert.Equal(new[] { "matters.read", "matters.write", "billing.read" }, permissions);
    }

    [Fact]
    public async Task Accept_NewUser_WithoutDetails_ReturnsAccountDetailsRequired()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);

        var result = await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, null, null, null));

        Assert.Equal(InvitationAcceptOutcome.AccountDetailsRequired, result.Outcome);
    }

    [Fact]
    public async Task Accept_ExistingUser_SkipsAccountCreationAndAddsMembership()
    {
        var repo = new FakeSaasRepository();
        var existingUserId = Guid.NewGuid();
        repo.SeedExistingUser("NEW.USER@EXAMPLE.COM", existingUserId);
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var creator = new FakeAccountCreator();
        var service = CreateService(repo, creator);

        var result = await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, null, null, null));

        Assert.Equal(InvitationAcceptOutcome.Success, result.Outcome);
        Assert.Null(creator.CreatedUserId); // existing user: no account creation
        Assert.Equal(existingUserId, result.UserId);
        Assert.Single(repo.Memberships);
    }

    [Fact]
    public async Task Accept_SecondTime_IsIdempotent_ReturnsAlreadyAccepted()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);
        await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        var second = await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        Assert.Equal(InvitationAcceptOutcome.AlreadyAccepted, second.Outcome);
    }

    [Fact]
    public async Task Accept_ExpiredInvitation_ReturnsExpired()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(repo);

        var result = await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        Assert.Equal(InvitationAcceptOutcome.Expired, result.Outcome);
    }

    [Fact]
    public async Task Accept_RevokedInvitation_ReturnsRevoked()
    {
        var repo = new FakeSaasRepository();
        repo.SeedInvitation(RawToken, InvitationStatus.Revoked, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);

        var result = await service.AcceptInvitationAsync(new AcceptInvitationRequest(RawToken, "Ada", "Lovelace", "Password123!"));

        Assert.Equal(InvitationAcceptOutcome.Revoked, result.Outcome);
    }

    [Fact]
    public async Task Accept_InvalidToken_ReturnsInvalidToken()
    {
        var service = CreateService(new FakeSaasRepository());

        var result = await service.AcceptInvitationAsync(new AcceptInvitationRequest("not-a-real-token", null, null, null));

        Assert.Equal(InvitationAcceptOutcome.InvalidToken, result.Outcome);
    }

    // ── Revoke ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Revoke_MarksInvitationRevoked()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);

        await service.RevokeInvitationAsync(TenantId, null, id);

        Assert.Equal(InvitationStatus.Revoked, repo.Invitations[id].StatusCode);
    }

    [Fact]
    public async Task Revoke_CrossTenant_IsForbidden()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(3));
        var service = CreateService(repo);

        await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.RevokeInvitationAsync(Guid.NewGuid(), null, id));
    }

    // ── Resend ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Resend_PendingInvitation_RotatesTokenAndReEnqueuesEmail()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(1));
        var originalHash = repo.Invitations[id].TokenHash;
        var service = CreateService(repo);

        await service.ResendInvitationAsync(TenantId, null, id);

        Assert.False(repo.Invitations[id].TokenHash.AsSpan().SequenceEqual(originalHash)); // token rotated
        Assert.Equal(InvitationStatus.Pending, repo.Invitations[id].StatusCode);
        var outbox = Assert.Single(repo.Outbox);
        Assert.Equal(InvitationService.InvitationEmailMessageType, outbox.MessageType);
    }

    [Fact]
    public async Task Resend_ExpiredInvitation_RotatesTokenAndReturnsToPending()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Expired, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(repo);

        await service.ResendInvitationAsync(TenantId, null, id);

        Assert.Equal(InvitationStatus.Pending, repo.Invitations[id].StatusCode);
        Assert.True(repo.Invitations[id].ExpiresAtUtc > DateTime.UtcNow); // expiry extended
    }

    [Fact]
    public async Task Resend_AcceptedInvitation_IsRejected()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Accepted, DateTime.UtcNow.AddDays(1));
        var service = CreateService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResendInvitationAsync(TenantId, null, id));
    }

    [Fact]
    public async Task Resend_CrossTenant_IsForbidden()
    {
        var repo = new FakeSaasRepository();
        var id = repo.SeedInvitation(RawToken, InvitationStatus.Pending, DateTime.UtcNow.AddDays(1));
        var service = CreateService(repo);

        await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.ResendInvitationAsync(Guid.NewGuid(), null, id));
    }

    // ── Test doubles ────────────────────────────────────────────────────────────
    private sealed class FakeAccountCreator : IUserAccountCreator
    {
        public Guid? CreatedUserId { get; private set; }

        public Task<Guid> CreateAsync(string email, string firstName, string lastName, string temporaryPassword, bool emailConfirmed, CancellationToken ct = default)
        {
            CreatedUserId = Guid.NewGuid();
            return Task.FromResult(CreatedUserId.Value);
        }

        public Task<Guid> InviteAsync(string email, string firstName, string lastName, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubConfiguration : IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private sealed record MembershipRecord(Guid TenantId, Guid UserId, Guid RoleId);

    private sealed class FakeSaasRepository : ISaasRepository
    {
        public Dictionary<Guid, InvitationEntry> Invitations { get; } = [];
        public List<OutboxMessageDto> Outbox { get; } = [];
        public List<MembershipRecord> Memberships { get; } = [];
        public Dictionary<Guid, List<Guid>> InvitationGroups { get; } = [];
        public List<(Guid GroupId, Guid UserId)> GroupMembers { get; } = [];

        private readonly Dictionary<string, Guid> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);

        public sealed record InvitationEntry(Guid InvitationId, Guid TenantId, string EmailNormalized, Guid RoleId, byte[] TokenHash, string StatusCode, DateTime ExpiresAtUtc);

        public void SeedExistingUser(string normalizedEmail, Guid userId) => _usersByEmail[normalizedEmail] = userId;

        public Guid SeedInvitation(string rawToken, string statusCode, DateTime expiresAtUtc, string email = "NEW.USER@EXAMPLE.COM")
        {
            var id = Guid.NewGuid();
            Invitations[id] = new InvitationEntry(id, TenantId, email, MemberRoleId, HashToken(rawToken), statusCode, expiresAtUtc);
            return id;
        }

        // Invitations ----------------------------------------------------------
        public Task<Guid> CreateInvitationAsync(Guid tenantId, string normalizedEmail, Guid roleId, byte[] tokenHash, DateTime expiresAtUtc, Guid? invitedByUserId, IReadOnlyList<Guid> additionalRoleIds, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            Invitations[id] = new InvitationEntry(id, tenantId, normalizedEmail, roleId, tokenHash, InvitationStatus.Pending, expiresAtUtc);
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TenantInvitationDto>>(
                Invitations.Values.Where(i => i.TenantId == tenantId).Select(ToDto).ToList());

        public Task<TenantInvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken ct = default)
            => Task.FromResult(Invitations.TryGetValue(invitationId, out var e) ? ToDto(e) : null);

        public Task<InvitationRow?> GetInvitationByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
        {
            var match = Invitations.Values.FirstOrDefault(i => i.TokenHash.AsSpan().SequenceEqual(tokenHash));
            return Task.FromResult<InvitationRow?>(match is null ? null
                : new InvitationRow(match.InvitationId, match.TenantId, match.EmailNormalized, match.RoleId, match.StatusCode, match.ExpiresAtUtc, null, null));
        }

        public Task<IReadOnlyList<Guid>> GetInvitationRoleIdsAsync(Guid invitationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Invitations.TryGetValue(invitationId, out var e) ? [e.RoleId] : []);

        public Task MarkInvitationAcceptedAsync(Guid invitationId, Guid acceptedByUserId, CancellationToken ct = default)
        {
            if (Invitations.TryGetValue(invitationId, out var e))
                Invitations[invitationId] = e with { StatusCode = InvitationStatus.Accepted };
            return Task.CompletedTask;
        }

        public Task RevokeInvitationAsync(Guid invitationId, Guid? actorUserId, CancellationToken ct = default)
        {
            if (Invitations.TryGetValue(invitationId, out var e))
                Invitations[invitationId] = e with { StatusCode = InvitationStatus.Revoked };
            return Task.CompletedTask;
        }

        public Task UpdateInvitationTokenAsync(Guid invitationId, byte[] tokenHash, DateTime expiresAtUtc, Guid? actorUserId, CancellationToken ct = default)
        {
            if (Invitations.TryGetValue(invitationId, out var e))
                Invitations[invitationId] = e with { TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc, StatusCode = InvitationStatus.Pending };
            return Task.CompletedTask;
        }

        // Outbox ---------------------------------------------------------------
        public Task<Guid> EnqueueOutboxAsync(string messageType, string payloadJson, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            Outbox.Add(new OutboxMessageDto(id, messageType, payloadJson, 0, 5));
            return Task.FromResult(id);
        }

        // Membership / lookups -------------------------------------------------
        public Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
            => Task.FromResult<Guid?>(string.Equals(roleCode, "MEMBER", StringComparison.OrdinalIgnoreCase) ? MemberRoleId : Guid.NewGuid());

        public Task<Guid?> FindUserIdByEmailAsync(string normalizedEmail, CancellationToken ct = default)
            => Task.FromResult(_usersByEmail.TryGetValue(normalizedEmail, out var id) ? id : (Guid?)null);

        public Task<Guid> UpsertMembershipAsync(Guid tenantId, Guid userId, Guid roleId, Guid? actorUserId, CancellationToken ct = default)
        {
            Memberships.Add(new MembershipRecord(tenantId, userId, roleId));
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>("Test Workspace");

        private static TenantInvitationDto ToDto(InvitationEntry e) => new(
            e.InvitationId, e.TenantId, e.EmailNormalized, e.RoleId, "MEMBER", "Member",
            e.StatusCode, null, e.ExpiresAtUtc, null, null, DateTime.UtcNow);

        // Groups ---------------------------------------------------------------
        public Task AddInvitationGroupsAsync(Guid invitationId, IReadOnlyList<Guid> groupIds, Guid? actorUserId, CancellationToken ct = default)
        {
            InvitationGroups[invitationId] = groupIds.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> GetInvitationGroupIdsAsync(Guid invitationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(InvitationGroups.TryGetValue(invitationId, out var g) ? g : []);

        public Task AddGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default)
        {
            GroupMembers.Add((groupId, userId));
            return Task.CompletedTask;
        }

        // ── Unused members (throw to surface accidental dependencies) ─────────
        public Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantGroupDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssignableRoleDto>> GetGroupRolesAsync(Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetGroupRoleIdsAsync(Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateGroupAsync(Guid tenantId, string name, string? description, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateGroupAsync(Guid tenantId, Guid groupId, string name, string? description, string statusCode, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteGroupAsync(Guid tenantId, Guid groupId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateVerificationChallengeAsync(Guid userId, string purpose, byte[] codeHash, DateTime expiresAtUtc, int maxAttempts, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VerificationChallengeRow?> GetActiveChallengeAsync(Guid userId, string purpose, CancellationToken ct = default) => throw new NotSupportedException();
        public Task IncrementChallengeAttemptAsync(Guid challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ConsumeChallengeAsync(Guid challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task TouchChallengeResendAsync(Guid challengeId, DateTime lastSentAtUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetProvisioningStatusAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertProvisioningStatusAsync(Guid userId, string statusCode, Guid? tenantId, string? failureReason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateTenantAsync(string name, string slug, Guid ownerUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateMembershipAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateTenantPlacementAsync(Guid tenantId, string regionCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantMembershipDto?> GetPrimaryMembershipAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default) => throw new NotSupportedException();

        public Guid? CapturedEffectiveUserId { get; private set; }
        public Guid? CapturedEffectiveTenantId { get; private set; }
        public IReadOnlyList<string> EffectivePermissions { get; set; } = [];
        public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            CapturedEffectiveUserId = userId;
            CapturedEffectiveTenantId = tenantId;
            return Task.FromResult(EffectivePermissions);
        }
        public Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ManagedMemberDto?> GetMemberAsync(Guid membershipId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool includeSystemRoles, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> FindActiveMembershipIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveOwnersAsync(Guid tenantId, Guid excludeMembershipId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateMembershipRoleAsync(Guid membershipId, Guid roleId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateMembershipStatusAsync(Guid membershipId, string statusCode, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveMembershipAsync(Guid membershipId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OutboxMessageDto>> DequeueOutboxBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkOutboxSentAsync(Guid outboxId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkOutboxFailedAsync(Guid outboxId, string error, DateTime nextAttemptUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> GetPlanIdByCodeAsync(string planCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateSubscriptionAsync(Guid tenantId, Guid planId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> GetActiveSubscriptionPlanIdAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EntitlementResolution>> ResolveEntitlementsAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CapabilityDefinition?> GetCapabilityAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CapabilityDefinition>> GetCapabilitiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> GetMonthlyUsageAsync(Guid tenantId, string meterCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecordUsageAsync(Guid tenantId, Guid? userId, Guid? executionId, Guid? matterId, string meterCode, string usageClass, decimal quantity, string? correlationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> ReserveUsageAsync(Guid tenantId, Guid? executionId, string meterCode, decimal quantity, DateTime expiresAtUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateReservationStatusAsync(Guid reservationId, string statusCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateExecutionAsync(Guid tenantId, Guid userId, Guid? matterId, string capabilityCode, string correlationId, Guid? parentExecutionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateExecutionStatusAsync(Guid executionId, string statusCode, string? failureCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IntelligenceExecutionDto?> GetExecutionAsync(Guid tenantId, Guid executionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> GetIdempotentExecutionAsync(Guid tenantId, string operation, string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateIdempotencyRecordAsync(Guid tenantId, string operation, string idempotencyKey, Guid executionId, DateTime expiresAtUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteAuditAsync(Guid? tenantId, Guid? userId, string eventType, Guid? executionId, string? resourceType, Guid? resourceId, string? dataJson, string? correlationId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
