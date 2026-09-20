using System.Text.Json;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Legal.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// OutboxDispatcher — transactional email drain (Phase B) tests.
//
// Verifies the retry-safe dispatch contract with an in-memory fake repository:
//   • successful send → MarkOutboxSent
//   • send failure    → MarkOutboxFailed with a future NextAttempt (backoff)
//   • backoff grows with attempt count and is capped at ~60 minutes
//   • unknown message types are treated as (non-fatal) failures, not sends
//   • the invitation payload is deserialized and forwarded to IJudzEmailSender
// ─────────────────────────────────────────────────────────────────────────────
public sealed class OutboxDispatcherTests
{
    private static OutboxDispatcher CreateDispatcher(FakeOutboxRepository repo, FakeEmailSender sender)
        => new(repo, sender, NullLogger<OutboxDispatcher>.Instance);

    private static string InvitationPayload(string email = "invitee@example.com")
        => JsonSerializer.Serialize(new InvitationEmailPayload(email, "Test Workspace", "Member", "https://app.judz.ai/invitations/abc"));

    [Fact]
    public async Task ProcessBatch_SuccessfulSend_MarksSentAndForwardsPayload()
    {
        var repo = new FakeOutboxRepository();
        var id = repo.Enqueue(InvitationService.InvitationEmailMessageType, InvitationPayload("ada@example.com"));
        var sender = new FakeEmailSender();

        var processed = await CreateDispatcher(repo, sender).ProcessBatchAsync(10);

        Assert.Equal(1, processed);
        Assert.Contains(id, repo.Sent);
        Assert.Empty(repo.Failed);
        var sent = Assert.Single(sender.SentInvitations);
        Assert.Equal("ada@example.com", sent.Email);
        Assert.Equal("https://app.judz.ai/invitations/abc", sent.AcceptUrl);
    }

    [Fact]
    public async Task ProcessBatch_SenderThrows_MarksFailedWithFutureBackoff()
    {
        var repo = new FakeOutboxRepository();
        var id = repo.Enqueue(InvitationService.InvitationEmailMessageType, InvitationPayload(), attemptCount: 0);
        var sender = new FakeEmailSender { ThrowOnSend = true };

        var processed = await CreateDispatcher(repo, sender).ProcessBatchAsync(10);

        Assert.Equal(0, processed);
        Assert.DoesNotContain(id, repo.Sent);
        var failure = Assert.Single(repo.Failed);
        Assert.Equal(id, failure.OutboxId);
        Assert.True(failure.NextAttemptUtc > DateTime.UtcNow); // rescheduled into the future
        Assert.False(string.IsNullOrWhiteSpace(failure.Error));
    }

    [Fact]
    public async Task ProcessBatch_BackoffGrowsWithAttemptCount_AndIsCappedAtOneHour()
    {
        var repo = new FakeOutboxRepository();
        // Attempt 1 → 2 minutes; attempt 20 → capped at 60 minutes.
        var early = repo.Enqueue(InvitationService.InvitationEmailMessageType, InvitationPayload(), attemptCount: 1);
        var late = repo.Enqueue(InvitationService.InvitationEmailMessageType, InvitationPayload(), attemptCount: 20);
        var sender = new FakeEmailSender { ThrowOnSend = true };
        var before = DateTime.UtcNow;

        await CreateDispatcher(repo, sender).ProcessBatchAsync(10);

        var earlyDelay = repo.Failed.Single(f => f.OutboxId == early).NextAttemptUtc - before;
        var lateDelay = repo.Failed.Single(f => f.OutboxId == late).NextAttemptUtc - before;
        Assert.True(lateDelay > earlyDelay);
        Assert.True(lateDelay <= TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(5)); // capped (+ tolerance)
    }

    [Fact]
    public async Task ProcessBatch_UnknownMessageType_IsFailedNotSent()
    {
        var repo = new FakeOutboxRepository();
        var id = repo.Enqueue("SomethingElse", "{}");
        var sender = new FakeEmailSender();

        var processed = await CreateDispatcher(repo, sender).ProcessBatchAsync(10);

        Assert.Equal(0, processed);
        Assert.Empty(sender.SentInvitations);
        Assert.Contains(repo.Failed, f => f.OutboxId == id);
    }

    [Fact]
    public async Task ProcessBatch_EmptyQueue_ReturnsZero()
    {
        var repo = new FakeOutboxRepository();

        var processed = await CreateDispatcher(repo, new FakeEmailSender()).ProcessBatchAsync(10);

        Assert.Equal(0, processed);
    }

    // ── Test doubles ────────────────────────────────────────────────────────────
    private sealed record SentInvitation(string Email, string TenantName, string RoleName, string AcceptUrl);
    private sealed record OutboxFailure(Guid OutboxId, string Error, DateTime NextAttemptUtc);

    private sealed class FakeEmailSender : IJudzEmailSender
    {
        public bool ThrowOnSend { get; set; }
        public List<SentInvitation> SentInvitations { get; } = [];

        public Task SendInvitationAsync(string email, string tenantName, string roleName, string acceptUrl, CancellationToken ct = default)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("SMTP unavailable.");
            SentInvitations.Add(new SentInvitation(email, tenantName, roleName, acceptUrl));
            return Task.CompletedTask;
        }

        public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeOutboxRepository : ISaasRepository
    {
        private readonly List<OutboxMessageDto> _queue = [];
        public List<Guid> Sent { get; } = [];
        public List<OutboxFailure> Failed { get; } = [];

        public Guid Enqueue(string messageType, string payloadJson, int attemptCount = 0, int maxAttempts = 5)
        {
            var id = Guid.NewGuid();
            _queue.Add(new OutboxMessageDto(id, messageType, payloadJson, attemptCount, maxAttempts));
            return id;
        }

        public Task<IReadOnlyList<OutboxMessageDto>> DequeueOutboxBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OutboxMessageDto>>(_queue.Take(batchSize).ToList());

        public Task MarkOutboxSentAsync(Guid outboxId, CancellationToken ct = default)
        {
            Sent.Add(outboxId);
            return Task.CompletedTask;
        }

        public Task MarkOutboxFailedAsync(Guid outboxId, string error, DateTime nextAttemptUtc, CancellationToken ct = default)
        {
            Failed.Add(new OutboxFailure(outboxId, error, nextAttemptUtc));
            return Task.CompletedTask;
        }

        // ── Unused members (throw to surface accidental dependencies) ─────────
        public Task<TenantProfileDto?> GetTenantProfileAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> TenantSlugExistsAsync(string slug, Guid excludeTenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateTenantProfileAsync(Guid tenantId, string name, string slug, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateVerificationChallengeAsync(Guid userId, string purpose, byte[] codeHash, DateTime expiresAtUtc, int maxAttempts, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantGroupDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssignableRoleDto>> GetGroupRolesAsync(Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetGroupRoleIdsAsync(Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateGroupAsync(Guid tenantId, string name, string? description, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateGroupAsync(Guid tenantId, Guid groupId, string name, string? description, string statusCode, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteGroupAsync(Guid tenantId, Guid groupId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddInvitationGroupsAsync(Guid invitationId, IReadOnlyList<Guid> groupIds, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetInvitationGroupIdsAsync(Guid invitationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VerificationChallengeRow?> GetActiveChallengeAsync(Guid userId, string purpose, CancellationToken ct = default) => throw new NotSupportedException();
        public Task IncrementChallengeAttemptAsync(Guid challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ConsumeChallengeAsync(Guid challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task TouchChallengeResendAsync(Guid challengeId, DateTime lastSentAtUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetProvisioningStatusAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertProvisioningStatusAsync(Guid userId, string statusCode, Guid? tenantId, string? failureReason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateTenantAsync(string name, string slug, Guid ownerUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateMembershipAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateTenantPlacementAsync(Guid tenantId, string regionCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantMembershipDto?> GetPrimaryMembershipAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MemberPageDto> PageMembersAsync(Guid? tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ManagedMemberDto?> GetMemberAsync(Guid membershipId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool includeSystemRoles, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> FindUserIdByEmailAsync(string normalizedEmail, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid?> FindActiveMembershipIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveOwnersAsync(Guid tenantId, Guid excludeMembershipId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> UpsertMembershipAsync(Guid tenantId, Guid userId, Guid roleId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateMembershipRoleAsync(Guid membershipId, Guid roleId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateMembershipStatusAsync(Guid membershipId, string statusCode, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveMembershipAsync(Guid membershipId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task ResetLockoutAsync(Guid membershipId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateInvitationAsync(Guid tenantId, string normalizedEmail, Guid roleId, byte[] tokenHash, DateTime expiresAtUtc, Guid? invitedByUserId, IReadOnlyList<Guid> additionalRoleIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantInvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InvitationRow?> GetInvitationByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetInvitationRoleIdsAsync(Guid invitationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkInvitationAcceptedAsync(Guid invitationId, Guid acceptedByUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeInvitationAsync(Guid invitationId, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateInvitationTokenAsync(Guid invitationId, byte[] tokenHash, DateTime expiresAtUtc, Guid? actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> EnqueueOutboxAsync(string messageType, string payloadJson, CancellationToken ct = default) => throw new NotSupportedException();
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
        public Task WriteAuditAsync(Guid? tenantId, Guid? userId, string eventType, Guid? executionId, string? resourceType, Guid? resourceId, string? dataJson, string? correlationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(Guid tenantId, DateTime sinceUtc, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditEventDto>> ListAuditEventsForUserAsync(Guid tenantId, Guid userId, DateTime sinceUtc, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResultDto<AuditEventDto>> PageAuditEventsForUserAsync(Guid tenantId, Guid userId, DateTime sinceUtc, string? search, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageAsync(Guid tenantId, DateTime sinceUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageForUserAsync(Guid tenantId, Guid userId, DateTime sinceUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecordLoginAsync(RecordLoginRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoginHistoryDto>> ListLoginHistoryAsync(Guid tenantId, DateTime sinceUtc, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecordConsentAsync(RecordConsentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConsentRecordDto>> ListConsentRecordsForUserAsync(Guid userId, Guid? tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
