namespace Legal.Application.Features.Intelligence.Decision.Core;

public enum EvidenceSourceType
{
    Unknown,
    CaseLaw,
    Statute,
    Regulation,
    AdministrativeAuthority,
    MatterDocument,
    Declaration,
    Deposition,
    Contract,
    Correspondence,
    BusinessRecord,
    SecondaryAuthority,
    GovernmentDocument,
}

public enum VerificationMethod
{
    NotEvaluated,
    ProfilePolicy,
    Deterministic,
    Provider,
    Retrieval,
    SemanticLlm,
    PoloxiSemanticDeepening,
    ErrorBoundary,
}

public static class VerificationFailureCodes
{
    public const string RetrievalFailed = "RETRIEVAL_FAILED";
    public const string SourceResolutionFailed = "SOURCE_RESOLUTION_FAILED";
    public const string CitationResolutionFailed = "CITATION_RESOLUTION_FAILED";
    public const string PassageNotLocated = "PASSAGE_NOT_LOCATED";
    public const string SemanticVerifierFailed = "SEMANTIC_VERIFIER_FAILED";
    public const string SemanticOutputInvalid = "SEMANTIC_OUTPUT_INVALID";
    public const string VerificationTimeout = "VERIFICATION_TIMEOUT";
    public const string VerificationBudgetExhausted = "VERIFICATION_BUDGET_EXHAUSTED";
    public const string AuthorityProviderUnavailable = "AUTHORITY_PROVIDER_UNAVAILABLE";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}

public enum EvidenceVerificationFactor
{
    Identity,
    Provenance,
    Citation,
    Passage,
    PropositionSupport,
    StatementRole,
    Holding,
    Authority,
}

public enum LegalStatementRole
{
    Unknown,
    CourtHolding,
    CourtReasoning,
    CourtFactualFinding,
    PartyArgument,
    PartyAllegation,
    ProceduralHistory,
    Background,
    QuotedAuthority,
    Dicta,
    Dissent,
    Concurrence,
}

public enum VerificationCheckState
{
    NotEvaluated,
    Passed,
    Failed,
    NotApplicable,
    Inconclusive,
    Error,
}

public enum PropositionSupportState
{
    NotEvaluated,
    Supported,
    PartiallySupported,
    Unsupported,
    Contradicted,
    Unverifiable,
    Error,
}

public enum EvidenceSupportDisposition
{
    Supported,
    PartiallySupported,
    Unsupported,
    Contradicted,
    Unverifiable,
    Error,
}

public sealed record VerificationCheckResult
{
    public required VerificationCheckState State { get; init; }
    public required string ReasonCode { get; init; }
    public string? Reason { get; init; }
    public string? VerifiedValue { get; init; }
    public string? SourceRef { get; init; }
    public string? SupportingPassage { get; init; }
    public required string VerificationMethod { get; init; }
    public string? PassageRef { get; init; }
    public string? VerifierId { get; init; }
    public string? VerifierVersion { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

    public static VerificationCheckResult NotEvaluated(string reasonCode, string reason) => new()
    {
        State = VerificationCheckState.NotEvaluated,
        ReasonCode = reasonCode,
        Reason = reason,
        VerificationMethod = "NOT_EVALUATED",
    };

    public static VerificationCheckResult NotApplicable(string reasonCode, string reason) => new()
    {
        State = VerificationCheckState.NotApplicable,
        ReasonCode = reasonCode,
        Reason = reason,
        VerificationMethod = "PROFILE_POLICY",
    };
}

public sealed record PropositionSupportResult
{
    public required PropositionSupportState State { get; init; }
    public required string Proposition { get; init; }
    public string? SupportingPassage { get; init; }
    public string? PassageRef { get; init; }
    public IReadOnlyList<string> SupportedComponents { get; init; } = [];
    public IReadOnlyList<string> UnsupportedComponents { get; init; } = [];
    public IReadOnlyList<string> ContradictedComponents { get; init; } = [];
    public required string ReasonCode { get; init; }
    public string? Reason { get; init; }
    public required string VerificationMethod { get; init; }
    public string? VerifierId { get; init; }
    public string? VerifierVersion { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

    public static PropositionSupportResult NotEvaluated(string proposition, string reasonCode, string reason) => new()
    {
        State = PropositionSupportState.NotEvaluated,
        Proposition = proposition,
        ReasonCode = reasonCode,
        Reason = reason,
        VerificationMethod = "NOT_EVALUATED",
    };
}

public sealed record VerificationProfile(
    string ProfileCode,
    bool RequireIdentity,
    bool RequireProvenance,
    bool RequireCitation,
    bool RequirePassage,
    bool RequirePropositionSupport,
    bool RequireStatementRole,
    bool RequireHolding,
    bool RequireAuthority)
{
    public int Version
    {
        get
        {
            var marker = ProfileCode.LastIndexOf("_V", StringComparison.Ordinal);
            return marker >= 0 && int.TryParse(ProfileCode.AsSpan(marker + 2), out var version) ? version : 1;
        }
    }

    public IReadOnlySet<EvidenceVerificationFactor> RequiredFactors => BuildRequiredFactors();
    public IReadOnlySet<EvidenceVerificationFactor> OptionalFactors { get; init; } = new HashSet<EvidenceVerificationFactor>();

    private IReadOnlySet<EvidenceVerificationFactor> BuildRequiredFactors()
    {
        var requirements = new Dictionary<EvidenceVerificationFactor, bool>
        {
            [EvidenceVerificationFactor.Identity] = RequireIdentity,
            [EvidenceVerificationFactor.Provenance] = RequireProvenance,
            [EvidenceVerificationFactor.Citation] = RequireCitation,
            [EvidenceVerificationFactor.Passage] = RequirePassage,
            [EvidenceVerificationFactor.PropositionSupport] = RequirePropositionSupport,
            [EvidenceVerificationFactor.StatementRole] = RequireStatementRole,
            [EvidenceVerificationFactor.Holding] = RequireHolding,
            [EvidenceVerificationFactor.Authority] = RequireAuthority,
        };
        return requirements.Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet();
    }
}

public sealed record EvidenceVerificationRequest(
    Guid DecisionEvidenceId,
    Guid? DecisionBranchId,
    string Proposition,
    string? SourceRef,
    string? SourceTitle,
    string? SourceText,
    EvidenceSourceType? DeclaredSourceType = null,
    string? Jurisdiction = null,
    DateOnly? AuthorityDate = null,
    DateOnly? CutoffDate = null,
    bool DecisionMaterial = false,
    string? CorrelationId = null)
{
    public string? SourceProvider { get; init; }
    public string? SourceVersion { get; init; }
    public string? PassageRef { get; init; }
    public string? ExtractionVersion { get; init; }
    public bool ProviderIdentityVerified { get; init; }
}

public sealed record EvidenceSourceSnapshot
{
    public required Guid SourceSnapshotId { get; init; }
    public required string ContentHash { get; init; }
    public required string PassageHash { get; init; }
    public string? SourceProvider { get; init; }
    public string? SourceVersion { get; init; }
    public string? SourceRef { get; init; }
    public string? PassageRef { get; init; }
    public string? ExtractionVersion { get; init; }
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SemanticVerificationResult
{
    public required PropositionSupportResult PropositionSupport { get; init; }
    public required VerificationCheckResult StatementRole { get; init; }
    public required VerificationCheckResult Holding { get; init; }
    public bool Ambiguous { get; init; }
    public int InputTokenCount { get; init; }
    public int OutputTokenCount { get; init; }
    public TimeSpan Duration { get; init; }
    public int CallCount { get; init; }
    public bool Deepened { get; init; }
    public bool CacheHit { get; init; }
}

public sealed record VerificationCandidatePreScreenResult(
    bool ShouldVerify,
    string ReasonCode,
    double Relevance,
    bool PotentialPassageAvailable);

public interface IVerificationCandidatePreScreen
{
    VerificationCandidatePreScreenResult Evaluate(EvidenceVerificationRequest request, EvidenceSourceType sourceType);
}

public sealed record PoloxiVerificationContract
{
    public required string Proposition { get; init; }
    public required EvidenceSourceType SourceType { get; init; }
    public required IReadOnlyList<string> Passages { get; init; }
    public required IReadOnlyList<PropositionSupportState> AllowedOutcomes { get; init; }
    public required IReadOnlyList<EvidenceVerificationFactor> SemanticFactors { get; init; }
    public required IReadOnlyList<string> UnresolvedDiscriminators { get; init; }
    public int MaxDeepeningRounds { get; init; } = 1;
    public int MaxInputTokens { get; init; }
    public int MaxOutputTokens { get; init; }
    public bool AllowExternalRetrieval { get; init; }
}

public interface ISemanticVerificationCache
{
    bool TryGet(string key, out SemanticVerificationResult result);
    void Store(string key, SemanticVerificationResult result);
}

public sealed record WebSourceInspectionResult
{
    public required bool Attempted { get; init; }
    public required bool Resolved { get; init; }
    public int? StatusCode { get; init; }
    public string? FinalUrl { get; init; }
    public string? DocumentTitle { get; init; }
    public string? VisibleText { get; init; }
    public string? FailureReason { get; init; }
    public required string VerificationMethod { get; init; }
}

public interface IWebSourceInspector
{
    Task<WebSourceInspectionResult> InspectAsync(string sourceRef, CancellationToken cancellationToken = default);
}

public sealed record EvidenceVerificationResult
{
    public required Guid DecisionEvidenceId { get; init; }
    public Guid? DecisionBranchId { get; init; }
    public required EvidenceSourceType SourceType { get; init; }
    public required VerificationProfile Profile { get; init; }
    public required VerificationCheckResult Identity { get; init; }
    public required VerificationCheckResult Provenance { get; init; }
    public required VerificationCheckResult Citation { get; init; }
    public required VerificationCheckResult Passage { get; init; }
    public required PropositionSupportResult PropositionSupport { get; init; }
    public required VerificationCheckResult StatementRole { get; init; }
    public required VerificationCheckResult Holding { get; init; }
    public required VerificationCheckResult Authority { get; init; }
    public required EvidenceSupportDisposition Disposition { get; init; }
    public bool IsVerified { get; init; }
    public bool IsDecisionAuthorized { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public VerificationTelemetry Telemetry { get; init; } = new();
    public EvidenceSourceSnapshot? SourceSnapshot { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record VerificationTelemetry
{
    public int RetrievedCount { get; init; }
    public int PreScreenRejectedCount { get; init; }
    public int MechanicalVerificationCount { get; init; }
    public int SemanticVerificationCount { get; init; }
    public int PoloxiDeepeningCount { get; init; }
    public int CacheHitCount { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public long TotalLatencyMilliseconds { get; init; }
}

public interface ISemanticEvidenceVerifier
{
    Task<SemanticVerificationResult> VerifyAsync(
        EvidenceVerificationRequest request,
        VerificationProfile profile,
        VerificationCheckResult passage,
        CancellationToken cancellationToken = default);
}

public interface IPoloxiVerificationDeepener
{
    Task<SemanticVerificationResult> DeepenAsync(
        PoloxiVerificationContract contract,
        SemanticVerificationResult current,
        CancellationToken cancellationToken = default);
}

public static class EvidenceVerificationInvariants
{
    public static bool GrantsPositiveDecisionAuthority(EvidenceVerificationResult result) =>
        result.IsVerified
        && result.PropositionSupport.State == PropositionSupportState.Supported
        && result.Disposition == EvidenceSupportDisposition.Supported
        && result.BlockingReasons.Count == 0;

    public static bool IsPositiveAuthorityForbidden(PropositionSupportState state) => state is
        PropositionSupportState.PartiallySupported or
        PropositionSupportState.Unsupported or
        PropositionSupportState.Contradicted or
        PropositionSupportState.Unverifiable or
        PropositionSupportState.NotEvaluated or
        PropositionSupportState.Error;

    public static bool IsRequiredFactorBlocking(VerificationCheckState state) => state is
        VerificationCheckState.NotEvaluated or
        VerificationCheckState.Failed or
        VerificationCheckState.Inconclusive or
        VerificationCheckState.Error;
}

public interface IEvidenceSourceClassifier
{
    EvidenceSourceType Classify(EvidenceVerificationRequest request);
}

public interface IVerificationProfileProvider
{
    VerificationProfile GetProfile(EvidenceSourceType sourceType);
}

public interface IEvidenceFactorVerifier
{
    EvidenceVerificationFactor Factor { get; }
}

public interface IIdentityEvidenceVerifier : IEvidenceFactorVerifier
{
    Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default);
}

public interface ICitationEvidenceVerifier : IEvidenceFactorVerifier
{
    Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default);
}

public interface IPassageEvidenceVerifier : IEvidenceFactorVerifier
{
    Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default);
}

public interface IPropositionSupportVerifier : IEvidenceFactorVerifier
{
    Task<PropositionSupportResult> VerifyAsync(EvidenceVerificationRequest request, VerificationCheckResult passage, CancellationToken cancellationToken = default);
}

public interface IHoldingEvidenceVerifier : IEvidenceFactorVerifier
{
    Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, VerificationCheckResult passage, CancellationToken cancellationToken = default);
}

public interface IAuthorityEvidenceVerifier : IEvidenceFactorVerifier
{
    Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default);
}

public interface IEvidenceVerificationAggregator
{
    EvidenceVerificationResult Aggregate(
        EvidenceVerificationRequest request,
        EvidenceSourceType sourceType,
        VerificationProfile profile,
        VerificationCheckResult identity,
        VerificationCheckResult provenance,
        VerificationCheckResult citation,
        VerificationCheckResult passage,
        PropositionSupportResult propositionSupport,
        VerificationCheckResult statementRole,
        VerificationCheckResult holding,
        VerificationCheckResult authority);
}

public interface IIndependentEvidenceVerificationPipeline
{
    Task<EvidenceVerificationResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default);
}
