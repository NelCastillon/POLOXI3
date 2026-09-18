using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-6 decision bridge (§34, §35).
//
// The seam that finally connects the governed EA layer (EA-1..EA-5) to the LIVE decision pipeline
// (LegalDecisionService). It is a strictly ADVISORY overlay:
//   1. Projects the already-computed V2 dependency graph (nodes/edges) into authoritative EA claims,
//      one ClaimProposition per assertion node (PROPOSITION/FACT), with support edges derived from
//      incoming SUPPORTS/ESTABLISHES/CONTRADICTS edges.
//   2. Runs each projected claim through EA-3 material verification (which applies the authority gate
//      and records an idempotent event). POLOXI — not the LLM — owns the resulting authority.
//   3. Runs EA-5 readiness evaluation and output audit over the persisted session claims.
//
// It NEVER mutates the V1/V2 status, readiness verdict, or answer. Every caller wraps it in a
// try/catch and continues on failure, honouring the repo rule that semantic features are optional and
// must fail without blocking core operations. Gated by UseEpistemicDecisionBridge.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A read-only projection of the V2 graph the live service already holds, plus session identity.
public sealed record EpistemicDecisionContext
{
    public required Guid SessionId { get; init; }

    public required Guid TenantId { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? ProposedByModel { get; init; }

    public string? PromptRunId { get; init; }

    public required IReadOnlyList<DecisionGraphNodeDto> Nodes { get; init; }

    public required IReadOnlyList<DecisionGraphEdgeDto> Edges { get; init; }
}

public sealed record EpistemicGovernanceResult
{
    public required bool Executed { get; init; }

    public int ProjectedClaimCount { get; init; }

    public int AuthorizedClaimCount { get; init; }

    public DecisionReadinessResult? Readiness { get; init; }

    public OutputClaimAuditResult? OutputAudit { get; init; }

    // EA-7: the override mode this run was governed under.
    public EpistemicOverrideMode OverrideMode { get; init; } = EpistemicOverrideMode.Advisory;

    // EA-7: EA's advisory readiness signal (true = EA considers the decision ready). In Advisory mode
    // this NEVER changes the authoritative V2 verdict — it is reference/annotation only.
    public bool EaReady { get; init; } = true;

    // EA-7: true only when the override mode is strong enough to actually change the effective verdict
    // AND EA downgraded readiness. Always false in Advisory mode.
    public bool OverrideApplied { get; init; }

    // EA-7: every claim the overlay touched (authorized AND unauthorized), annotated for the UI so all
    // involved information stays visible for reference.
    public IReadOnlyList<InvolvedClaim> InvolvedClaims { get; init; } = [];

    public IReadOnlyList<string> AuditNarrative { get; init; } = [];

    public static EpistemicGovernanceResult Skipped(string reason) => new()
    {
        Executed = false,
        AuditNarrative = [reason],
    };
}

// A single claim surfaced for UI annotation: the text, its verification state, whether POLOXI
// authorized it, and a human-readable annotation. Nothing is hidden — unauthorized claims are shown
// with the reason they cannot be relied upon.
public sealed record InvolvedClaim
{
    public required Guid ClaimId { get; init; }

    public required string Text { get; init; }

    public required ClaimVerificationState VerificationState { get; init; }

    public required ClaimDecisionAuthority DecisionAuthority { get; init; }

    public required bool IsAuthorized { get; init; }

    public required bool IsEssential { get; init; }

    public required string Annotation { get; init; }
}

public interface IEpistemicDecisionBridge
{
    Task<EpistemicGovernanceResult> ProjectAndGovernAsync(
        EpistemicDecisionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class EpistemicDecisionBridge(
    IMaterialClaimVerificationService verificationService,
    IDecisionReadinessEvaluator readinessEvaluator,
    IOutputClaimAuditor outputAuditor,
    EpistemicAuthoritySettings settings,
    ILogger<EpistemicDecisionBridge> logger)
    : IEpistemicDecisionBridge
{
    // Only assertion nodes become claims; evidence/authority/structural nodes become support edges.
    private static readonly HashSet<string> ClaimNodeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        DecisionGraphNodeKinds.Proposition,
        DecisionGraphNodeKinds.Fact,
    };

    public async Task<EpistemicGovernanceResult> ProjectAndGovernAsync(
        EpistemicDecisionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!settings.UseEpistemicDecisionBridge)
            return EpistemicGovernanceResult.Skipped(
                "Epistemic decision bridge disabled (UseEpistemicDecisionBridge=false).");

        var narrative = new List<string>();
        var nodesById = context.Nodes.ToDictionary(n => n.NodeId);

        // 1. Project + verify every assertion node into an authoritative EA claim.
        var projectedClaimIds = new List<Guid>();
        var authorizedClaimIds = new List<Guid>();
        var involvedClaims = new List<InvolvedClaim>();
        foreach (var node in context.Nodes.Where(n => ClaimNodeKinds.Contains(n.NodeKind)))
        {
            var support = BuildSupport(node, context, nodesById);
            var claim = BuildClaim(node, context, support);

            var outcome = await verificationService.VerifyAsync(new ClaimVerificationRequest
            {
                Claim = claim,
                Support = support,
                TenantId = context.TenantId,
                ActorUserId = context.ActorUserId,
                Reason = "EA-6 projection from live V2 decision graph.",
            }, cancellationToken);

            projectedClaimIds.Add(claim.ClaimId);
            var isAuthorized = outcome.Authority.DecisionAuthority != ClaimDecisionAuthority.None;
            if (isAuthorized)
                authorizedClaimIds.Add(claim.ClaimId);

            // Keep EVERY claim (authorized or not) visible for reference, annotated with its status.
            involvedClaims.Add(new InvolvedClaim
            {
                ClaimId = claim.ClaimId,
                Text = claim.Text,
                VerificationState = outcome.NewState,
                DecisionAuthority = outcome.Authority.DecisionAuthority,
                IsAuthorized = isAuthorized,
                IsEssential = claim.IsEssential,
                Annotation = isAuthorized
                    ? $"POLOXI-authorized ({ClaimCodes.ToCode(outcome.NewState)}); may be relied upon."
                    : $"Not authorized ({ClaimCodes.ToCode(outcome.NewState)}, authority=None); shown for reference only.",
            });
        }

        narrative.Add($"Projected {projectedClaimIds.Count} assertion node(s) into authoritative claims; "
            + $"{authorizedClaimIds.Count} gained POLOXI authority.");

        // 2. Readiness governance over the persisted session claims (advisory only).
        var readiness = await readinessEvaluator.EvaluateAsync(
            context.SessionId, context.TenantId, cancellationToken);
        narrative.AddRange(readiness.AuditNarrative);

        // 3. Output audit over the claims that could surface in the answer (all projected claims).
        var outputAudit = await outputAuditor.AuditAsync(
            context.SessionId, context.TenantId, projectedClaimIds, cancellationToken);
        narrative.AddRange(outputAudit.AuditNarrative);

        // 4. EA-7 override computation. Downgrade-only and mode-gated. In Advisory mode the effective
        //    verdict is NEVER changed — EA only annotates. SoftGate/HardGate may downgrade a ready
        //    verdict when EA found blockers, but never upgrade and never mutate the V2 verdict.
        var eaReady = readiness.IsReady;
        var overrideApplied = settings.OverrideMode != EpistemicOverrideMode.Advisory
            && readiness.Enforced
            && !eaReady;
        narrative.Add(overrideApplied
            ? $"Override mode {EpistemicOverrideModes.ToCode(settings.OverrideMode)}: EA downgraded readiness to NOT-ready."
            : $"Override mode {EpistemicOverrideModes.ToCode(settings.OverrideMode)}: advisory only; V2 verdict unchanged.");

        logger.LogInformation(
            "EA-7 bridge governed session {SessionId}: {Projected} claim(s), {Authorized} authorized, "
            + "readiness={Ready}, output-clean={Clean}, mode={Mode}, overrideApplied={Override}.",
            context.SessionId, projectedClaimIds.Count, authorizedClaimIds.Count,
            readiness.IsReady, outputAudit.IsClean, EpistemicOverrideModes.ToCode(settings.OverrideMode), overrideApplied);

        return new EpistemicGovernanceResult
        {
            Executed = true,
            ProjectedClaimCount = projectedClaimIds.Count,
            AuthorizedClaimCount = authorizedClaimIds.Count,
            Readiness = readiness,
            OutputAudit = outputAudit,
            OverrideMode = settings.OverrideMode,
            EaReady = eaReady,
            OverrideApplied = overrideApplied,
            InvolvedClaims = involvedClaims,
            AuditNarrative = narrative,
        };
    }

    // Build the authoritative claim shell for an assertion node. POLOXI owns state/authority; the
    // node's advisory signals seed materiality/impact only.
    private static ClaimProposition BuildClaim(
        DecisionGraphNodeDto node,
        EpistemicDecisionContext context,
        IReadOnlyList<ClaimSupportRef> support)
    {
        var text = string.IsNullOrWhiteSpace(node.Statement) ? node.DisplayName : node.Statement!;
        return new ClaimProposition
        {
            ClaimId = node.NodeId,
            SessionId = context.SessionId,
            MatterId = context.MatterId,
            Text = text,
            NormalizedText = Normalize(text),
            ClaimType = MapClaimType(node.NodeKind),
            Origin = ClaimOrigin.LlmGenerated,
            VerificationState = ClaimVerificationState.Proposed,
            DecisionAuthority = ClaimDecisionAuthority.None,
            Materiality = node.Support,
            DecisionImpact = node.Support,
            IsEssential = node.IsEssential,
            SupportingEvidence = support.Where(s => s.Relationship != ClaimSupportRelationship.Contradicts).ToArray(),
            ContradictingEvidence = support.Where(s => s.Relationship == ClaimSupportRelationship.Contradicts).ToArray(),
            ProposedByModel = context.ProposedByModel,
            PromptRunId = context.PromptRunId,
            Version = 0,
        };
    }

    // Derive claim support from incoming edges. Only VERIFIED edges count as independently verified;
    // INVALIDATED edges become contradictions; UNVERIFIED edges are asserted-but-unverified support.
    private static IReadOnlyList<ClaimSupportRef> BuildSupport(
        DecisionGraphNodeDto node,
        EpistemicDecisionContext context,
        IReadOnlyDictionary<Guid, DecisionGraphNodeDto> nodesById)
    {
        var support = new List<ClaimSupportRef>();
        foreach (var edge in context.Edges.Where(e => e.TargetNodeId == node.NodeId))
        {
            var isVerified = string.Equals(edge.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase);
            var isInvalidated = string.Equals(edge.VerificationStatus, DecisionVerificationStates.Invalidated, StringComparison.OrdinalIgnoreCase);
            var isContradiction = isInvalidated
                || string.Equals(edge.RelationCode, DecisionGraphRelations.Contradicts, StringComparison.OrdinalIgnoreCase);

            var sourceIsAuthority = nodesById.TryGetValue(edge.SourceNodeId, out var src)
                && string.Equals(src.NodeKind, DecisionGraphNodeKinds.Strategy, StringComparison.OrdinalIgnoreCase);

            support.Add(new ClaimSupportRef
            {
                SupportId = edge.EdgeId,
                ClaimId = node.NodeId,
                EvidenceId = sourceIsAuthority ? null : edge.SourceNodeId,
                AuthorityId = sourceIsAuthority ? edge.SourceNodeId : null,
                Relationship = isContradiction ? ClaimSupportRelationship.Contradicts : ClaimSupportRelationship.Supports,
                Strength = edge.SupportWeight,
                // Only an independently VERIFIED, non-contradicting edge grants verified strength.
                IndependentlyVerified = isVerified && !isContradiction,
                VerificationReason = edge.VerificationNotes,
            });
        }

        return support;
    }

    private static ClaimType MapClaimType(string nodeKind)
        => string.Equals(nodeKind, DecisionGraphNodeKinds.Fact, StringComparison.OrdinalIgnoreCase)
            ? ClaimType.Factual
            : ClaimType.Interpretive;

    private static string Normalize(string text)
        => string.Join(' ', text.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
