namespace Legal.Web.Services.CommandCenter;

// ─────────────────────────────────────────────────────────────────────────────
//  Case + Decision Command Center view-model.
//
//  This is a READ-ONLY composition layer for the hackathon UI. It assembles the
//  Clio discovery snapshot (read-only) and the EXISTING Judz decision read models
//  into a single normalized shape the dashboard binds to. It introduces NO new
//  domain logic and NEVER lets the Clio matter stage become a POLOXI signal — the
//  stage is carried only as presentation/context (see MatterLifecycleContext).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Provenance stamp attached to every imported fact so the UI can drill to source.</summary>
public sealed record SourceProvenance(
    string SourceSystem,          // e.g. "Clio"
    string SourceObjectType,      // e.g. "Matter", "CustomField", "Contact", "Document"
    string? SourceObjectId = null,
    DateTimeOffset? ObservedAt = null)
{
    public static SourceProvenance Clio(string objectType, string? objectId = null) =>
        new("Clio", objectType, objectId, DateTimeOffset.UtcNow);

    public static SourceProvenance Judz(string objectType, string? objectId = null) =>
        new("Judz", objectType, objectId, DateTimeOffset.UtcNow);
}

/// <summary>
/// The Clio matter stage as first-class CONTEXT. Influences presentation/filtering only.
/// It must never independently modify POLOXI candidate scores.
/// </summary>
public sealed record MatterLifecycleContext(
    string SourceSystem,
    string? SourceMatterId,
    string? PracticeArea,
    string? StageName,
    int? StageSequence,
    DateTimeOffset ImportedAt)
{
    /// <summary>The canonical Personal Injury stage order supplied by the challenge.</summary>
    public static readonly IReadOnlyList<string> PersonalInjuryStages =
    [
        "Intake", "Treatment", "Demand", "Negotiation",
        "Litigation", "Trial", "Disbursement", "Closed"
    ];

    public static int? SequenceFor(string? stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName)) return null;
        for (var i = 0; i < PersonalInjuryStages.Count; i++)
            if (string.Equals(PersonalInjuryStages[i], stageName, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return null;
    }
}

public sealed record MatterIdentity(
    Guid MatterId,
    string Title,
    string? ClientName,
    string? PracticeArea,
    string? ClioDisplayNumber,
    long? ClioMatterId);

public sealed record MatterParty(
    string DisplayName,
    string? SourceRole,
    string? NormalizedRole,
    bool IsClient,
    bool IsAdverseParty,
    bool IsProvider,
    bool RequiresReview,
    SourceProvenance Provenance);

public sealed record MatterFact(
    string Name,
    string? Value,
    string? FieldType,
    string? NormalizedField,     // the structured Judz field it mapped to, when any
    SourceProvenance Provenance);

public sealed record MatterEventItem(
    string Title,
    DateTimeOffset? OccurredAt,
    string? Category,            // Accident, Treatment, Discovery, Deadline, etc.
    string? Detail,
    string? RelatedPropositionId,
    string? DecisionRelevance,   // High/Medium/Low — only when derived from decision data
    SourceProvenance Provenance);

public sealed record MatterDocumentRef(
    string Name,
    long? ClioDocumentId,
    Guid? JudzDocumentId,
    string? Category,
    string? Folder,
    string? LatestVersion,
    SourceProvenance Provenance);

public sealed record CasePulseItem(
    int Rank,
    string Title,
    string? Detail,
    string? DecisionRelevance,   // null when not decision-derived
    string? RelatedPropositionId,
    SourceProvenance? Provenance);

public sealed record SinceLastReviewItem(
    string Kind,                 // NEW MEDICAL RECORD, DISCOVERY RESPONSE, PROVIDER UPDATE, ...
    string Title,
    string? Detail,
    DateTimeOffset? OccurredAt,
    bool IsMaterial,
    SourceProvenance? Provenance);

public sealed record KeyDateItem(
    string Label,
    DateTimeOffset? Date,
    string? Countdown,           // e.g. "14 days"
    string? Tone);               // normal / warning / danger

public sealed record TreatmentStage(
    string Name,                 // Accident, Initial Eval, Radiology, ...
    string? Provider,
    string? Dates,
    int? RecordCount,
    string? Status);

public sealed record EconomicsLine(
    string Label,
    decimal? Amount,
    string? Note,
    string Group);               // MEDICAL / CASE / COVERAGE / RECOVERY

// ── Decision Intelligence summary (POLOXI). Values are nullable by design: they
//    appear ONLY when an authoritative session/competition actually executed. ──

public enum DecisionAnalysisState
{
    NotRun,          // no decision session exists yet
    Interpretive,    // leading interpretation exists, competition NOT executed
    Calculated       // authoritative POLOXI competition executed
}

public sealed record DecisionCandidate(
    string Code,                 // C1, C2, ...
    string Label,
    double? Probability,         // null unless Calculated
    string? Rationale);

public sealed record DecisionUncertainty(
    string Code,                 // U1, U2, ...
    string Title,
    string? Detail,
    string Severity);            // HIGH / MEDIUM / LOW

public sealed record DecisionChangeFactor(
    string Code,                 // P17, P23, ...
    string Title,
    string Impact);              // HIGH / MEDIUM / LOW

public sealed record ResolveNextTarget(
    string PropositionId,
    string Title,
    string ResolutionQuestion,
    string? MissingInformation,
    double? InformationValue,
    double? FlipPotential,
    double? LegalAdv);

public sealed record DecisionIntelligenceSummary(
    DecisionAnalysisState State,
    Guid? DecisionSessionId,
    string? CurrentOutcome,
    int DocumentCount,
    int EvidenceCount,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<DecisionCandidate> Candidates,
    IReadOnlyList<DecisionUncertainty> Uncertainties,
    IReadOnlyList<DecisionChangeFactor> ChangeFactors,
    ResolveNextTarget? ResolveNext,
    string ResolveNextState,     // Calculated / NotCalculated / Blocked / NoneRequired
    string? ResolveNextReason);

/// <summary>Health/provenance banner for demo-reliability (graceful degradation).</summary>
public sealed record SnapshotHealth(
    DateTimeOffset? LastClioSync,
    bool ClioConnected,
    bool DecisionSessionPresent,
    string? Notice)
{
    /// <summary>When this snapshot's data was actually composed.</summary>
    public DateTimeOffset BuiltAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>True when this snapshot was served from the resilience cache, not a fresh build.</summary>
    public bool ServedFromCache { get; init; }

    /// <summary>Human-readable age of the cached data when served stale (e.g. "3 minutes ago").</summary>
    public string? CachedAgeText { get; init; }
}

/// <summary>
/// Domain-pack participation diagnostics (spec §7). Derived from the domain-pack
/// codes actually stamped on ingested Judz documents — never asserted blindly.
/// </summary>
public sealed record DomainPackDiagnostics(
    string PackCode,             // e.g. PERSONAL_INJURY
    bool Applied,                // true only when at least one document carries the pack
    int DocumentsTagged,         // how many ingested docs reference this pack
    string? MatterType);

public sealed record MatterIntelligenceSnapshot(
    MatterIdentity Identity,
    MatterLifecycleContext Lifecycle,
    IReadOnlyList<MatterParty> Parties,
    IReadOnlyList<MatterFact> Facts,
    IReadOnlyList<MatterEventItem> Timeline,
    IReadOnlyList<TreatmentStage> Treatment,
    IReadOnlyList<EconomicsLine> Economics,
    IReadOnlyList<MatterDocumentRef> Documents,
    IReadOnlyList<CasePulseItem> CasePulse,
    IReadOnlyList<SinceLastReviewItem> SinceLastReview,
    IReadOnlyList<KeyDateItem> KeyDates,
    DecisionIntelligenceSummary Decision,
    DomainPackDiagnostics? DomainPack,
    SnapshotHealth Health)
{
    /// <summary>Attorney-approved provider sharing policies for this matter (DB-backed).</summary>
    public IReadOnlyList<ProviderSharingSummary> ProviderSharing { get; init; } = [];

    /// <summary>
    /// The review boundary used to compute <see cref="SinceLastReview"/>: the moment the
    /// attorney last opened this matter before the current build. Null on first open.
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; init; }
}

/// <summary>Per-provider sharing summary surfaced in the attorney Command Center.</summary>
public sealed record ProviderSharingSummary(
    string ProviderKey,
    string ProviderDisplayName,
    bool PortalEnabled,
    bool ShareMatterStatus,
    bool ShareCurrentStage,
    bool SharePatientTreatment,
    bool ShareOwnRecords,
    bool ShareOwnBills,
    bool ShareFirmRequests,
    bool ShareOtherProviders,
    bool ShareSettlementInfo,
    int OpenRequestCount);