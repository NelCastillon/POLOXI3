using System.Security.Cryptography;
using System.Text;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-3 material-claim verification service (§18, §27, §33).
//
// Bridges a claim's verification outcome into the existing V2.1 dependency-propagation loop WITHOUT
// duplicating it. Responsibilities:
//   1. Aggregate only INDEPENDENTLY-VERIFIED support/contradiction strength (RetrievedSource ≠ Verified).
//   2. Deterministically classify the new ClaimVerificationState via the single ClaimStateMachine path,
//      routing pre-resolution states through VerificationInProgress and never inventing illegal jumps.
//   3. Persist the authoritative claim, its support edges, and an idempotent verification event.
//   4. Map the resolved claim state onto the DecisionVerificationStates edge code the propagation
//      service consumes, so a claim verification can change WHICH investigation POLOXI runs next.
//
// The graph mutation itself stays in IDependencyPropagationService; EA-3 only decides the edge status
// and signals (exactly once, via idempotency) that propagation should run.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// The verified evidence gathered for a claim this round (support edges already resolved by retrieval).
public sealed record ClaimVerificationRequest
{
    public required ClaimProposition Claim { get; init; }

    // The full, authoritative support set for the claim after this verification round.
    public IReadOnlyList<ClaimSupportRef> Support { get; init; } = [];

    public required Guid TenantId { get; init; }

    public Guid? ActorUserId { get; init; }

    // Optional edge this claim maps onto in the decision graph, so the caller can run propagation.
    public Guid? DependencyEdgeId { get; init; }

    public string? Reason { get; init; }
}

public sealed record ClaimVerificationOutcome
{
    public required ClaimProposition Claim { get; init; }

    public required ClaimVerificationState PreviousState { get; init; }

    public required ClaimVerificationState NewState { get; init; }

    // True when the state actually changed (a legal, non-noop transition was applied).
    public bool StateChanged { get; init; }

    // True only when a NEW verification event row was written (idempotent — run propagation once).
    public bool VerificationEventRecorded { get; init; }

    // The edge verification code (DecisionVerificationStates.*) the propagation service should apply.
    public required string MappedEdgeStatus { get; init; }

    // The authority decision recomputed for the resolved claim.
    public required ClaimAuthorityDecision Authority { get; init; }

    public Guid? DependencyEdgeId { get; init; }

    public required ClaimVerificationChangedEvent Event { get; init; }
}

public interface IMaterialClaimVerificationService
{
    Task<ClaimVerificationOutcome> VerifyAsync(
        ClaimVerificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class MaterialClaimVerificationService(
    IEpistemicClaimRepository repository,
    IClaimAuthorityGate authorityGate,
    EpistemicAuthoritySettings settings)
    : IMaterialClaimVerificationService
{
    public async Task<ClaimVerificationOutcome> VerifyAsync(
        ClaimVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Claim);

        var previous = request.Claim.VerificationState;

        // 1. Split support into supporting vs contradicting; only independently-verified edges count
        //    toward strength. Asserted-but-unverified support must NOT grant authority.
        var supporting = request.Support
            .Where(s => s.Relationship != ClaimSupportRelationship.Contradicts)
            .ToArray();
        var contradicting = request.Support
            .Where(s => s.Relationship == ClaimSupportRelationship.Contradicts)
            .ToArray();

        var supportStrength = MaxVerifiedStrength(supporting);
        var contradictionStrength = MaxVerifiedStrength(contradicting);

        // 2. Deterministic classification through the single ClaimStateMachine path.
        var classified = ClaimStateMachine.Classify(
            supportStrength,
            contradictionStrength,
            settings.VerificationAdequacyThreshold);

        var newState = ResolveTransition(previous, classified);

        // 3. Build the authoritative, resolved claim.
        var resolved = request.Claim with
        {
            VerificationState = newState,
            VerificationStrength = supportStrength,
            SupportingEvidence = supporting,
            ContradictingEvidence = contradicting,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            VerificationReason = request.Reason ?? request.Claim.VerificationReason,
            Version = request.Claim.Version + 1,
        };

        // §34 feature flag: the authority gate can be disabled in isolation. When off, no LLM claim
        // may gain authority — the safe default is None (never the reverse).
        var authority = settings.UseClaimAuthorityGate
            ? authorityGate.Evaluate(resolved, settings.ToAuthorityContext())
            : new ClaimAuthorityDecision
            {
                ClaimId = resolved.ClaimId,
                VerificationState = resolved.VerificationState,
                DecisionAuthority = ClaimDecisionAuthority.None,
                MayInfluenceCompetition = false,
                Reason = "Authority gate disabled (UseClaimAuthorityGate=false).",
            };
        var withAuthority = resolved with { DecisionAuthority = authority.DecisionAuthority };

        // 4. Persist claim + support edges.
        await repository.UpsertClaimAsync(
            ClaimPersistenceMapper.ToPersistence(withAuthority, request.TenantId, request.ActorUserId),
            cancellationToken);

        var supportRows = request.Support
            .Select(s => ClaimPersistenceMapper.ToPersistence(s, request.TenantId, request.ActorUserId))
            .ToArray();
        await repository.ReplaceSupportAsync(
            withAuthority.ClaimId, supportRows, request.TenantId, request.ActorUserId, cancellationToken);

        // 5. Idempotent verification event — only a new row signals propagation should run.
        var changed = BuildEvent(withAuthority, previous, newState, supporting, contradicting, request);
        var recorded = await repository.TryRecordVerificationEventAsync(
            ClaimPersistenceMapper.ToPersistence(changed, request.TenantId, request.ActorUserId),
            cancellationToken);

        return new ClaimVerificationOutcome
        {
            Claim = withAuthority,
            PreviousState = previous,
            NewState = newState,
            StateChanged = previous != newState,
            VerificationEventRecorded = recorded,
            MappedEdgeStatus = MapToEdgeStatus(newState),
            Authority = authority,
            DependencyEdgeId = request.DependencyEdgeId,
            Event = changed,
        };
    }

    // Only independently-verified edges contribute strength; asserted support is ignored here.
    private static decimal MaxVerifiedStrength(IReadOnlyList<ClaimSupportRef> support)
    {
        decimal max = 0m;
        foreach (var s in support)
        {
            if (!s.IndependentlyVerified)
                continue;
            if (s.Strength > max)
                max = s.Strength;
        }
        return max;
    }

    // Routes pre-resolution states through VerificationInProgress and refuses illegal jumps: if the
    // classified target is not legally reachable, the claim keeps its current state (no fabricated move).
    private static ClaimVerificationState ResolveTransition(
        ClaimVerificationState current,
        ClaimVerificationState classified)
    {
        var from = current is ClaimVerificationState.Proposed or ClaimVerificationState.VerificationRequired
            ? ClaimVerificationState.VerificationInProgress
            : current;

        return ClaimStateMachine.CanTransition(from, classified) ? classified : current;
    }

    // AbsenceOfSupport ≠ EvidenceOfFalsity is preserved by the state machine; the mapping only
    // translates a resolved verification state onto the graph edge verification vocabulary.
    private static string MapToEdgeStatus(ClaimVerificationState state) => state switch
    {
        ClaimVerificationState.Supported => DecisionVerificationStates.Verified,
        ClaimVerificationState.Contradicted => DecisionVerificationStates.Invalidated,
        _ => DecisionVerificationStates.Unverified,
    };

    private static ClaimVerificationChangedEvent BuildEvent(
        ClaimProposition claim,
        ClaimVerificationState previous,
        ClaimVerificationState newState,
        IReadOnlyList<ClaimSupportRef> supporting,
        IReadOnlyList<ClaimSupportRef> contradicting,
        ClaimVerificationRequest request)
    {
        var evidenceIds = request.Support
            .Where(s => s.EvidenceId is not null)
            .Select(s => s.EvidenceId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var authorityIds = request.Support
            .Where(s => s.AuthorityId is not null)
            .Select(s => s.AuthorityId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        return new ClaimVerificationChangedEvent
        {
            EventId = Guid.NewGuid(),
            ClaimId = claim.ClaimId,
            PreviousState = previous,
            NewState = newState,
            EvidenceIds = evidenceIds,
            AuthorityIds = authorityIds,
            Reason = request.Reason
                ?? $"Verified: support={supporting.Count}, contradiction={contradicting.Count}.",
            IdempotencyKey = BuildIdempotencyKey(claim.ClaimId, previous, newState, evidenceIds, authorityIds),
            OccurredAt = DateTimeOffset.UtcNow,
        };
    }

    // Deterministic key bounded to fit IdempotencyKey NVARCHAR(200): replaying the same
    // claim/transition/evidence set yields exactly one event. The evidence/authority sets are hashed
    // so an unbounded number of GUIDs can never overflow (or truncate-collide) the column.
    private static string BuildIdempotencyKey(
        Guid claimId,
        ClaimVerificationState previous,
        ClaimVerificationState newState,
        IReadOnlyList<Guid> evidenceIds,
        IReadOnlyList<Guid> authorityIds)
    {
        var payload = $"E[{string.Join(",", evidenceIds)}]:A[{string.Join(",", authorityIds)}]";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return $"{claimId:N}:{ClaimCodes.ToCode(previous)}->{ClaimCodes.ToCode(newState)}:{digest}";
    }
}
