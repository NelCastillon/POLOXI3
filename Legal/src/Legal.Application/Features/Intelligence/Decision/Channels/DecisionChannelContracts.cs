namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Decision Channels — contract-first boundary (slice 1: DocumentEvidence).
//
// A CHANNEL is not a decision engine. It is a source of information that, once validated by that
// channel's OWN validator (DocumentEvidence → AER; Legal Authority → authority verification; Human
// Intelligence → attorney attribution/approval; Investigation → its own verification), produces one
// or more DecisionContributions targeting specific nodes of the AUTHORITATIVE persisted hierarchy.
//
// STRICT BOUNDARY (the whole point of this layer):
//   * A DecisionContribution is QUALITATIVE source-truth: a target hierarchy node, a channel type, a
//     relation (SUPPORTS / CONTRADICTS / …), a verification state, and full provenance.
//   * It carries NO numeric ImpactScore and NO signal delta. POLOXI Wide2 remains the SOLE owner of
//     candidate competition, uncertainty, IV, convergence, and outcome. Verified contributions feed
//     the EXISTING POLOXI signal-resolution / recompetition; they never run a parallel algorithm.
//   * The derived node signal/state and any DecisionImpact are SEPARATE, reproducible OUTPUTS, never
//     stored back onto the contribution.
//
// The boundary type (DecisionContribution) is validator-agnostic so additional channels plug in
// without being forced through evidence/AER semantics.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Which information channel produced a contribution. Extensible: only DocumentEvidence is wired in
// slice 1, but the vocabulary is defined up front so downstream tables/UI are channel-complete.
public enum DecisionChannelType
{
    DocumentEvidence = 0,
    LegalAuthority = 1,
    HumanIntelligence = 2,
    Investigation = 3,
    DecisionContract = 4,
    ExternalResearch = 5,
}

// The qualitative relationship a contribution asserts against its target hierarchy node. This is the
// ONLY "strength" a contribution carries — no numbers. POLOXI derives the effective node signal.
public enum ContributionRelation
{
    // The channel information tends to establish/support the node proposition.
    Supports = 0,
    // The channel information tends to contradict/undermine the node proposition.
    Contradicts = 1,
    // The channel information supports the node only under stated limits/conditions.
    Qualifies = 2,
    // The channel information (typically Human Intelligence) challenges the node, reopening verification.
    Challenges = 3,
    // The channel information establishes a new fact the node depends on.
    Establishes = 4,
    // The channel information invalidates a prior basis for the node.
    Invalidates = 5,
    // The channel information is relevant context only; it does not move support by itself.
    ContextOnly = 6,
    // The channel information is present but insufficient to support the node.
    Insufficient = 7,
}

// The verification lifecycle of a contribution AS DETERMINED BY ITS CHANNEL'S OWN VALIDATOR. Only a
// Verified contribution may feed positive support into POLOXI signal resolution (mirrors the core
// DecisionSupportVerificationState invariant). Contradicting/challenging contributions remain
// meaningful even when the underlying fact is verified against the opposing proposition.
public enum ContributionVerificationState
{
    Unverified = 0,
    Verified = 1,
    Refuted = 2,
    Disputed = 3,
}

// Immutable provenance for a contribution: where it came from and how it was produced, so we never
// treat unverified model output as authoritative and can always reconstruct WHY a node's state moved.
public sealed record ContributionProvenance
{
    // The channel's own source object (e.g. a Legal_SourceAssertion id for DocumentEvidence, an
    // authority id, an attorney action id). Kept as an opaque reference + type so the boundary stays
    // channel-agnostic; each channel knows how to resolve its own SourceType.
    public required string SourceTypeCode { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceLabel { get; init; }

    // For DocumentEvidence: which document/version/passage the assertion came from (nullable for other channels).
    public Guid? LegalDocumentId { get; init; }
    public Guid? LegalDocumentVersionId { get; init; }
    public Guid? LegalDocumentPassageId { get; init; }

    // Model/prompt provenance when the contribution was proposed by an LLM stage (advisory until verified).
    public string? ProposedByModel { get; init; }
    public string? PromptRunId { get; init; }

    // Free-text reason the channel's validator recorded for the verification state (auditable).
    public string? VerificationReason { get; init; }
}

// The single validator-agnostic object every channel produces at the POLOXI boundary. Qualitative
// source-truth only — bound to exactly one node of a persisted authoritative hierarchy execution.
public sealed record DecisionContribution
{
    public Guid ContributionId { get; init; } = Guid.NewGuid();

    public required Guid TenantId { get; init; }
    public required Guid DecisionMatterId { get; init; }

    // Authoritative binding: the run-scoped hierarchy node this contribution targets. Both are
    // required because HierarchyNodeId is never a cross-run identity (see migration 0366).
    public required Guid HierarchyExecutionId { get; init; }
    public required Guid HierarchyNodeId { get; init; }

    public required DecisionChannelType ChannelType { get; init; }
    public required ContributionRelation Relation { get; init; }
    public required ContributionVerificationState VerificationState { get; init; }

    // Which existing POLOXI signal this contribution informs (e.g. EvidenceSupport, FactSupport,
    // AuthoritySupport, Uncertainty). Maps to the existing DecisionSupportSignal vocabulary; POLOXI
    // resolves the effective value — the contribution never asserts a numeric level.
    public required string TargetSignalCode { get; init; }

    // Applicability/coverage/directness are OPTIONAL qualitative qualifiers (not scores) some channels
    // record (e.g. how directly an authority governs the node). Left null when a channel has no notion.
    public string? ApplicabilityCode { get; init; }
    public string? DirectnessCode { get; init; }

    // OPTIONAL normalized magnitude in [0,1] expressing HOW STRONGLY the channel's own judgment places
    // this contribution WITHIN the frame it was decided in. For Human Intelligence this is the attorney's
    // relative position inside the sibling band ((ConfirmedValue - lower) / (upper - lower)) — the
    // intelligence encoded by WHERE the attorney inserted (or overwrote) the proposition. It is NOT a
    // score: POLOXI still owns the composite/ceiling/entropy/margin math and the final consequence. When
    // null (all legacy channels, or no comparable band), the adapter falls back to the fixed magnitude so
    // existing behavior is byte-identical.
    public double? Magnitude { get; init; }

    // Effectivity window for time-scoped contributions (e.g. superseded authority). Null = always.
    public DateTime? EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }

    public required ContributionProvenance Provenance { get; init; }

    public Guid? ActorUserId { get; init; }

    // True only when this contribution is allowed to feed positive support into POLOXI signal
    // resolution: it must be Verified. Contradicts/Challenges/Invalidates remain meaningful regardless.
    public bool ContributesPositiveSupport
        => VerificationState == ContributionVerificationState.Verified
           && Relation is ContributionRelation.Supports or ContributionRelation.Establishes or ContributionRelation.Qualifies;
}

// The authoritative POLOXI lineage a contribution's target hierarchy node resolves to. A run-scoped
// HierarchyNodeId is NOT itself a POLOXI BranchId/CandidateId (see migration 0366 — the hierarchy node
// carries no such column), so lineage must be resolved SEPARATELY and supplied to the adapter. A
// contribution whose node resolves to neither a branch nor a candidate produces no signal (fail-soft):
// the channel boundary never moves the ranking without a real dependency link.
public sealed record ChannelContributionLineage(
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> CandidateIds)
{
    public static ChannelContributionLineage None { get; } = new([], []);

    public bool HasLineage => BranchIds.Count > 0 || CandidateIds.Count > 0;
}

// Resolves a contribution's run-scoped target node to authoritative POLOXI branch/candidate ids. This
// is the ONLY place the node→branch/candidate mapping lives; the adapter stays a pure projection and
// never guesses lineage itself. The infrastructure implementation (persisted mapping) is out of scope
// for this slice and must return ChannelContributionLineage.None when no authoritative link exists.
public interface IChannelContributionLineageResolver
{
    ChannelContributionLineage Resolve(DecisionContribution contribution);
}

// A channel resolves validated information into contributions against the authoritative hierarchy.
// Each implementation uses its OWN validator; none is forced through evidence/AER semantics.
public interface IDecisionChannel
{
    // The channel this implementation represents.
    DecisionChannelType ChannelType { get; }

    // Resolve zero or more qualitative contributions for a matter's authoritative hierarchy. Returns
    // an empty collection (fail-soft) when there is nothing to contribute or no authoritative
    // hierarchy exists yet — it must never fabricate a node or a signal.
    Task<IReadOnlyList<DecisionContribution>> ResolveContributionsAsync(
        DecisionChannelResolveContext context,
        CancellationToken cancellationToken = default);
}

// Input a channel needs to resolve contributions. Kept minimal for slice 1; extended per channel.
public sealed record DecisionChannelResolveContext
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid DecisionMatterId { get; init; }

    // The authoritative hierarchy execution the contributions must bind to (resolved by the caller so
    // the channel does not have to know how authority is promoted). Null = no authoritative hierarchy.
    public Guid? AuthoritativeHierarchyExecutionId { get; init; }

    // Channel-specific source anchor (e.g. the uploaded document version for DocumentEvidence).
    public Guid? SourceId { get; init; }
    public Guid? SourceVersionId { get; init; }
    public string? SourceHash { get; init; }
    public string? SourceLabel { get; init; }
    public DateTime? SourceDateUtc { get; init; }

    // The resolved Domain Pack for this matter (synonym terminology + evidence-type→signal map +
    // entity/event vocabularies). Null when no pack is configured or resolution failed fail-soft.
    // Channels treat it as advisory: synonyms only ADD matches, the signal map only overrides the
    // default target signal when a mapping exists. POLOXI Wide2 still owns candidate competition.
    public ResolvedDomainPack? ResolvedPack { get; init; }
}

// Stable string codes at the persistence boundary (mirror the *Code columns in Legal_ChannelContribution).
public static class DecisionChannelCodes
{
    public static string ToCode(DecisionChannelType channel) => channel switch
    {
        DecisionChannelType.LegalAuthority => "LegalAuthority",
        DecisionChannelType.HumanIntelligence => "HumanIntelligence",
        DecisionChannelType.Investigation => "Investigation",
        DecisionChannelType.DecisionContract => "DecisionContract",
        DecisionChannelType.ExternalResearch => "ExternalResearch",
        _ => "DocumentEvidence",
    };

    public static DecisionChannelType ToChannel(string? code) => code switch
    {
        "LegalAuthority" => DecisionChannelType.LegalAuthority,
        "HumanIntelligence" => DecisionChannelType.HumanIntelligence,
        "Investigation" => DecisionChannelType.Investigation,
        "DecisionContract" => DecisionChannelType.DecisionContract,
        "ExternalResearch" => DecisionChannelType.ExternalResearch,
        _ => DecisionChannelType.DocumentEvidence,
    };

    public static string ToCode(ContributionRelation relation) => relation switch
    {
        ContributionRelation.Contradicts => "CONTRADICTS",
        ContributionRelation.Qualifies => "QUALIFIES",
        ContributionRelation.Challenges => "CHALLENGES",
        ContributionRelation.Establishes => "ESTABLISHES",
        ContributionRelation.Invalidates => "INVALIDATES",
        ContributionRelation.ContextOnly => "CONTEXT_ONLY",
        ContributionRelation.Insufficient => "INSUFFICIENT",
        _ => "SUPPORTS",
    };

    public static ContributionRelation ToRelation(string? code) => code switch
    {
        "CONTRADICTS" => ContributionRelation.Contradicts,
        "QUALIFIES" => ContributionRelation.Qualifies,
        "CHALLENGES" => ContributionRelation.Challenges,
        "ESTABLISHES" => ContributionRelation.Establishes,
        "INVALIDATES" => ContributionRelation.Invalidates,
        "CONTEXT_ONLY" => ContributionRelation.ContextOnly,
        "INSUFFICIENT" => ContributionRelation.Insufficient,
        _ => ContributionRelation.Supports,
    };

    public static string ToCode(ContributionVerificationState state) => state switch
    {
        ContributionVerificationState.Verified => "Verified",
        ContributionVerificationState.Refuted => "Refuted",
        ContributionVerificationState.Disputed => "Disputed",
        _ => "Unverified",
    };

    public static ContributionVerificationState ToVerificationState(string? code) => code switch
    {
        "Verified" => ContributionVerificationState.Verified,
        "Refuted" => ContributionVerificationState.Refuted,
        "Disputed" => ContributionVerificationState.Disputed,
        _ => ContributionVerificationState.Unverified,
    };

    // The existing POLOXI decision-support signal vocabulary the contributions inform. Kept as string
    // codes so the channel boundary does not depend on the Epistemic enum shape.
    public static class TargetSignal
    {
        public const string EvidenceSupport = "EvidenceSupport";
        public const string FactSupport = "FactSupport";
        public const string AuthoritySupport = "AuthoritySupport";
        public const string LegalSupport = "LegalSupport";
        public const string Uncertainty = "Uncertainty";
    }
}

// Flat persistence row mirroring POLOXI.Legal_ChannelContribution (migration 0367). Qualitative
// source-truth only: NO numeric score / signal-delta column by design.
public sealed record ChannelContributionPersistence(
    Guid ChannelContributionId,
    Guid DecisionMatterId,
    Guid HierarchyExecutionId,
    Guid HierarchyNodeId,
    string ChannelTypeCode,
    string RelationCode,
    string VerificationStateCode,
    string TargetSignalCode,
    string? ApplicabilityCode,
    string? DirectnessCode,
    string SourceTypeCode,
    Guid? SourceId,
    string? SourceLabel,
    Guid? LegalDocumentId,
    Guid? LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? ProposedByModel,
    string? PromptRunId,
    string? VerificationReason,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    // OPTIONAL normalized [0,1] relative-position qualifier (not a score); null for channels/rows
    // with no comparable band. See DecisionContribution.Magnitude and migration 0374.
    double? PlacementMagnitude,
    Guid TenantId,
    Guid? ActorUserId);

// Read DTO for the Decision Channel provenance surface ("why is this node's state what it is?").
// Projects one persisted contribution for API/UI without exposing base/audit noise.
public sealed record ChannelContributionDto(
    Guid ChannelContributionId,
    Guid DecisionMatterId,
    Guid HierarchyExecutionId,
    Guid HierarchyNodeId,
    string ChannelTypeCode,
    string RelationCode,
    string VerificationStateCode,
    string TargetSignalCode,
    string? ApplicabilityCode,
    string? DirectnessCode,
    string SourceTypeCode,
    Guid? SourceId,
    string? SourceLabel,
    Guid? LegalDocumentId,
    Guid? LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string? ProposedByModel,
    string? PromptRunId,
    string? VerificationReason,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    double? PlacementMagnitude,
    DateTime CreatedDateUtc);

// Rehydrates a persisted contribution (ChannelContributionDto) back into the validator-agnostic
// DecisionContribution boundary object so it can be RE-PROJECTED into typed recompetition. This never
// re-validates and never invents a score: the persisted RelationCode/VerificationStateCode/TargetSignal
// are the source-truth. TenantId is supplied by the (tenant-scoped) caller since the read DTO omits it.
public static class ChannelContributionRehydration
{
    public static DecisionContribution ToContribution(ChannelContributionDto dto, Guid tenantId) => new()
    {
        ContributionId = dto.ChannelContributionId,
        TenantId = tenantId,
        DecisionMatterId = dto.DecisionMatterId,
        HierarchyExecutionId = dto.HierarchyExecutionId,
        HierarchyNodeId = dto.HierarchyNodeId,
        ChannelType = DecisionChannelCodes.ToChannel(dto.ChannelTypeCode),
        Relation = DecisionChannelCodes.ToRelation(dto.RelationCode),
        VerificationState = DecisionChannelCodes.ToVerificationState(dto.VerificationStateCode),
        TargetSignalCode = dto.TargetSignalCode,
        ApplicabilityCode = dto.ApplicabilityCode,
        DirectnessCode = dto.DirectnessCode,
        Magnitude = dto.PlacementMagnitude,
        EffectiveFromUtc = dto.EffectiveFromUtc,
        EffectiveToUtc = dto.EffectiveToUtc,
        Provenance = new ContributionProvenance
        {
            SourceTypeCode = dto.SourceTypeCode,
            SourceId = dto.SourceId,
            SourceLabel = dto.SourceLabel,
            LegalDocumentId = dto.LegalDocumentId,
            LegalDocumentVersionId = dto.LegalDocumentVersionId,
            LegalDocumentPassageId = dto.LegalDocumentPassageId,
            ProposedByModel = dto.ProposedByModel,
            PromptRunId = dto.PromptRunId,
            VerificationReason = dto.VerificationReason,
        },
    };
}
