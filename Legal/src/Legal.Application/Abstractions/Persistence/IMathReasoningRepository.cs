namespace Legal.Application.Abstractions.Persistence;

// ── POLOXI Mathematics V1 — run persistence / audit seam ─────────────────────────────────────────
// Persists a completed Math solve run for reproducibility and audit: the execution header, the
// deterministic per-obligation verdicts, and the verification-weighted ranked candidates. Persistence
// is best-effort and never alters the honest response the verifier already produced.
public interface IMathReasoningRepository
{
    // Persists a whole solve run (header + obligations + candidates) atomically. Returns the new
    // MathExecutionId so callers can correlate follow-up reads.
    Task<Guid> SaveMathExecutionAsync(MathExecutionRecord execution, CancellationToken cancellationToken = default);

    // Lists recent Math solve runs for a tenant (most recent first) for the audit history view.
    Task<IReadOnlyList<MathExecutionSummary>> ListMathExecutionsAsync(Guid tenantId, int take = 50, CancellationToken cancellationToken = default);

    // Loads a single Math run with its obligation and candidate audit rows. Returns null when the
    // execution is not found for the tenant.
    Task<MathExecutionDetail?> GetMathExecutionAsync(Guid tenantId, Guid mathExecutionId, CancellationToken cancellationToken = default);
}

// Lightweight header projection for the run-history list.
public sealed record MathExecutionSummary
{
    public required Guid MathExecutionId { get; init; }

    public required string ProblemText { get; init; }

    public required string CorrelationId { get; init; }

    public required string OutcomeCode { get; init; }

    public required string VerificationStatusCode { get; init; }

    public string? CanonicalAnswer { get; init; }

    public decimal DiscoveryConfidence { get; init; }

    public decimal SelfConsistencyAgreement { get; init; }

    public long? DurationMilliseconds { get; init; }

    public required DateTime CreatedDateUtc { get; init; }
}

// Full run projection: the header plus its deterministic obligation verdicts and ranked candidates.
public sealed record MathExecutionDetail
{
    public required Guid MathExecutionId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid UserId { get; init; }

    public required string ProblemText { get; init; }

    public required string CorrelationId { get; init; }

    public required string OutcomeCode { get; init; }

    public required string VerificationStatusCode { get; init; }

    public string? FinalAnswer { get; init; }

    public string? CanonicalAnswer { get; init; }

    public string? SolutionSummary { get; init; }

    public string? RemainingUncertainty { get; init; }

    public decimal DiscoveryConfidence { get; init; }

    public decimal SelfConsistencyAgreement { get; init; }

    public string? ModelCode { get; init; }

    public long? DurationMilliseconds { get; init; }

    public required DateTime CreatedDateUtc { get; init; }

    public decimal ResearchPriority { get; init; }

    public decimal EvidenceSupport { get; init; }

    public decimal MathematicalVerification { get; init; }

    public decimal FalsificationCoverage { get; init; }

    public string? EpistemicStateLabel { get; init; }

    public IReadOnlyList<MathObligationRecord> Obligations { get; init; } = [];

    public IReadOnlyList<MathCandidateRecord> Candidates { get; init; } = [];
}

// Execution header plus its child audit rows.
public sealed record MathExecutionRecord
{
    public required Guid TenantId { get; init; }

    public required Guid UserId { get; init; }

    public required string ProblemText { get; init; }

    public required string CorrelationId { get; init; }

    public required string OutcomeCode { get; init; }

    public required string VerificationStatusCode { get; init; }

    public string? FinalAnswer { get; init; }

    public string? CanonicalAnswer { get; init; }

    public string? SolutionSummary { get; init; }

    public string? RemainingUncertainty { get; init; }

    public decimal DiscoveryConfidence { get; init; }

    public decimal SelfConsistencyAgreement { get; init; }

    public string? ModelCode { get; init; }

    public long? DurationMilliseconds { get; init; }

    // POLOXI epistemic hardening: the four orthogonal dimensions (R/E/V/F) plus the derived label, so
    // research priority and evidence are audited separately from mathematical verification.
    public decimal ResearchPriority { get; init; }

    public decimal EvidenceSupport { get; init; }

    public decimal MathematicalVerification { get; init; }

    public decimal FalsificationCoverage { get; init; }

    public string? EpistemicStateLabel { get; init; }

    public IReadOnlyList<MathObligationRecord> Obligations { get; init; } = [];

    public IReadOnlyList<MathCandidateRecord> Candidates { get; init; } = [];
}

public sealed record MathObligationRecord
{
    public required string ObligationKey { get; init; }

    public required string Statement { get; init; }

    public required string VerificationMethodCode { get; init; }

    public required string StatusCode { get; init; }

    public required string CounterexampleStatusCode { get; init; }

    public decimal DiscoveryConfidence { get; init; }

    public string? VerificationNote { get; init; }

    public int SortOrder { get; init; }
}

public sealed record MathCandidateRecord
{
    public required string CandidateKey { get; init; }

    public required string ObjectTypeCode { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public decimal DiscoveryConfidence { get; init; }

    public required string VerificationStatusCode { get; init; }

    public int SortOrder { get; init; }
}
