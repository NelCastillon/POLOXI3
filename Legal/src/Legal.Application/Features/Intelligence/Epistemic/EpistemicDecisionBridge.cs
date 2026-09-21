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

    public string? FinalAnswer { get; init; }

    public IReadOnlyList<ComposerClaimProvenance> ComposerProvenance { get; init; } = [];
}

public static class OutputClaimExtractionStates
{
    public const string NotAttempted = "NOT_ATTEMPTED";
    public const string Completed = "COMPLETED";
    public const string Empty = "EMPTY";
    public const string Failed = "FAILED";
}

public sealed record OutputClaimExtractionResult
{
    public bool Attempted { get; init; }
    public int SourceLength { get; init; }
    public bool SubstantiveAnswer { get; init; }
    public int ClaimsReturned { get; init; }
    public string StatusCode { get; init; } = OutputClaimExtractionStates.NotAttempted;
    public string? FailureReason { get; init; }
}

public sealed record EpistemicGovernanceResult
{
    public required bool Executed { get; init; }

    public int ProjectedClaimCount { get; init; }

    public int AuthorizedClaimCount { get; init; }

    public DecisionReadinessResult? Readiness { get; init; }

    public OutputClaimAuditResult? OutputAudit { get; init; }

    public OutputClaimExtractionResult ClaimExtraction { get; init; } = new();

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

    public IReadOnlyList<OutputClaimLedgerEntry> ClaimLedger { get; init; } = [];

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

    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }
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
    IClaimExtractor claimExtractor,
    IClaimIdentityResolver identityResolver,
    IOutputClaimProvenanceReconciler provenanceReconciler,
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
                SourceBranchId = claim.SourceBranchId,
                SourceCandidateId = claim.SourceCandidateId,
                Annotation = isAuthorized
                    ? $"POLOXI-authorized ({ClaimCodes.ToCode(outcome.NewState)}); may be relied upon."
                    : $"Not authorized ({ClaimCodes.ToCode(outcome.NewState)}, authority=None); shown for reference only.",
            });
        }

        var extraction = await ExtractOutputClaimsAsync(
            context, involvedClaims, claimExtractor, identityResolver, provenanceReconciler, cancellationToken);
        narrative.Add(extraction.FailureReason ??
            $"Output claim extraction {extraction.Result.StatusCode}: {extraction.Result.ClaimsReturned} material claim(s) from {extraction.Result.SourceLength} characters.");

        narrative.Add($"Projected {projectedClaimIds.Count} assertion node(s) into authoritative claims; "
            + $"{authorizedClaimIds.Count} gained POLOXI authority.");

        // 2. Readiness governance over the persisted session claims (advisory only).
        var readiness = await readinessEvaluator.EvaluateAsync(
            context.SessionId, context.TenantId, cancellationToken);
        narrative.AddRange(readiness.AuditNarrative);

        // 3. Output audit over claims actually extracted from the composed answer.
        var outputClaimIds = involvedClaims
            .Where(c => extraction.ExtractedClaimIds.Contains(c.ClaimId))
            .Select(c => c.ClaimId)
            .ToArray();
        var authoritativeAudit = await outputAuditor.AuditAsync(
            context.SessionId, context.TenantId, outputClaimIds, cancellationToken);
        var outputAudit = ApplyMappingInvariants(authoritativeAudit, extraction.Ledger);
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
            ClaimExtraction = extraction.Result,
            OverrideMode = settings.OverrideMode,
            EaReady = eaReady,
            OverrideApplied = overrideApplied,
            InvolvedClaims = involvedClaims,
            ClaimLedger = extraction.Ledger,
            AuditNarrative = narrative,
        };
    }

    private static async Task<(OutputClaimExtractionResult Result, HashSet<Guid> ExtractedClaimIds, IReadOnlyList<OutputClaimLedgerEntry> Ledger, string? FailureReason)> ExtractOutputClaimsAsync(
        EpistemicDecisionContext context,
        IReadOnlyList<InvolvedClaim> authoritativeClaims,
        IClaimExtractor extractor,
        IClaimIdentityResolver identityResolver,
        IOutputClaimProvenanceReconciler provenanceReconciler,
        CancellationToken cancellationToken)
    {
        var source = context.FinalAnswer ?? string.Empty;
        var substantive = !string.IsNullOrWhiteSpace(source);
        if (!substantive)
            return (new OutputClaimExtractionResult { SourceLength = source.Length }, [], [], null);

        try
        {
            var proposals = await extractor.ExtractAsync(new ClaimExtractionContext
            {
                SessionId = context.SessionId,
                MatterId = context.MatterId,
                SourceText = source,
                ModelName = context.ProposedByModel,
                PromptRunId = context.PromptRunId,
                ComposerProvenance = context.ComposerProvenance,
            }, cancellationToken);
            var existing = authoritativeClaims.Select(c => new ClaimProposition
            {
                ClaimId = c.ClaimId,
                SessionId = context.SessionId,
                Text = c.Text,
                NormalizedText = identityResolver.Normalize(c.Text),
                ClaimType = ClaimType.Interpretive,
                VerificationState = c.VerificationState,
                DecisionAuthority = c.DecisionAuthority,
                IsEssential = c.IsEssential,
                SourceBranchId = c.SourceBranchId,
                SourceCandidateId = c.SourceCandidateId,
            }).ToArray();
            var reconciled = provenanceReconciler.Reconcile(proposals, existing, context.ComposerProvenance);
            var matched = reconciled
                .Where(p => p.MappingState == ClaimMappingState.Mapped && p.SourcePropositionId.HasValue)
                .Select(p => p.SourcePropositionId!.Value)
                .ToHashSet();
            var ledger = reconciled.Select(p => new OutputClaimLedgerEntry
            {
                OutputClaimId = p.SourcePropositionId ?? StableOutputClaimId(context.SessionId, p.ClaimKey, p.Text),
                ClaimText = p.Text,
                IsMaterial = p.IsMaterial,
                SourcePropositionId = p.SourcePropositionId,
                EvidenceAttachmentIds = p.EvidenceAttachmentIds,
                DecisionEvidenceIds = p.DecisionEvidenceIds,
                MappingState = p.MappingState,
                Disposition = p.MappingState == ClaimMappingState.Mapped
                    ? OutputClaimDisposition.Allow
                    : OutputClaimDispositions.EnforceMappingInvariant(p.IsMaterial, p.MappingState, OutputClaimDisposition.Allow),
                ReasonCode = p.MappingReasonCode ?? "OUTPUT_CLAIM_MAPPING_NOT_EVALUATED",
            }).ToArray();
            var status = proposals.Count == 0 ? OutputClaimExtractionStates.Empty : OutputClaimExtractionStates.Completed;
            var reason = proposals.Count == 0
                ? "OUTPUT_CLAIM_EXTRACTION_EMPTY: substantive answer produced zero material claims."
                : matched.Count == 0
                    ? "OUTPUT_CLAIM_EXTRACTION_UNMAPPED: material claims were found but none matched an authoritative graph claim."
                    : null;
            return (new OutputClaimExtractionResult
            {
                Attempted = true,
                SourceLength = source.Length,
                SubstantiveAnswer = true,
                ClaimsReturned = proposals.Count,
                StatusCode = status,
                FailureReason = reason,
            }, matched, ledger, reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var reason = $"OUTPUT_CLAIM_EXTRACTION_FAILED: {ex.GetType().Name}.";
            return (new OutputClaimExtractionResult
            {
                Attempted = true,
                SourceLength = source.Length,
                SubstantiveAnswer = true,
                StatusCode = OutputClaimExtractionStates.Failed,
                FailureReason = reason,
            }, [], [], reason);
        }
    }

    private static OutputClaimAuditResult ApplyMappingInvariants(
        OutputClaimAuditResult audit,
        IReadOnlyList<OutputClaimLedgerEntry> ledger)
    {
        var blocked = ledger.Where(entry => entry.IsMaterial && entry.MappingState != ClaimMappingState.Mapped).ToArray();
        if (blocked.Length == 0)
            return audit;

        var synthetic = blocked.Select(entry => new OutputClaimAuthorization
        {
            ClaimId = entry.OutputClaimId,
            ClaimText = entry.ClaimText,
            VerificationState = ClaimVerificationState.Unverifiable,
            DecisionAuthority = ClaimDecisionAuthority.None,
            Disposition = entry.Disposition,
            IsForeign = entry.MappingState == ClaimMappingState.Unmapped,
            Reason = entry.ReasonCode,
        }).ToArray();
        var violations = blocked.Select(entry => new OutputClaimViolation
        {
            ClaimId = entry.OutputClaimId,
            ClaimText = entry.ClaimText,
            VerificationState = ClaimVerificationState.Unverifiable,
            DecisionAuthority = ClaimDecisionAuthority.None,
            Reason = entry.ReasonCode,
        }).ToArray();
        return audit with
        {
            IsClean = false,
            Authorizations = audit.Authorizations.Concat(synthetic).ToArray(),
            Violations = audit.Violations.Concat(violations).ToArray(),
            UnknownClaimIds = audit.UnknownClaimIds.Concat(blocked
                .Where(entry => entry.MappingState == ClaimMappingState.Unmapped)
                .Select(entry => entry.OutputClaimId)).Distinct().ToArray(),
            AuditNarrative = audit.AuditNarrative.Concat([
                $"Claim reconciliation blocked ALLOW for {blocked.Length} material unmapped or scope-exceeding claim(s)."
            ]).ToArray(),
        };
    }

    private static Guid StableOutputClaimId(Guid sessionId, string claimKey, string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{sessionId:N}|{claimKey}|{text}"));
        return new Guid(bytes.AsSpan(0, 16));
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
