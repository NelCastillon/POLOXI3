using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application.Abstractions.Intelligence;

public enum LegalSearchOperationKind
{
    ExactAuthority=0,
    Lexical=1,
    Semantic=2,
    CitationExpansion=3,
    TargetedFallback=4,
}

public enum LegalRetrievalOutcome
{
    ResultsFound=0,
    NoResults=1,
    ProviderFailure=2,
    AccessDenied=3,
    CoverageGap=4,
    InvalidQuery=5,
    FilteredOut=6,
    ParsingFailure=7,
    RequiredScopeMissing=8,
    ScopeUnsupported=9,
}

public sealed record LegalResearchRequest(
    Guid TenantId,
    Guid DecisionSessionId,
    DecisionResearchNeedPersistence ResearchNeed,
    string? Jurisdiction,
    DateOnly AuthorityCutoffDate,
    int MaximumOperations,
    int MaximumAuthorities)
{
    public LegalAuthorityScope? AuthorityScope { get; init; }
}

public sealed record LegalSearchOperation(
    Guid LegalSearchOperationId,
    LegalSearchOperationKind Kind,
    string Query,
    LegalAuthorityKind AuthorityKind,
    int Sequence,
    Guid? ParentOperationId=null);

public sealed record LegalSearchPlan(
    Guid LegalSearchPlanId,
    Guid DecisionResearchNeedId,
    Guid DecisionSessionId,
    Guid TenantId,
    string Proposition,
    string? Jurisdiction,
    DateOnly AuthorityCutoffDate,
    IReadOnlyList<LegalSearchOperation> Operations)
{
    public Guid AtomicPropositionId { get; init; }
    public LegalAuthorityScope? AuthorityScope { get; init; }
}

public sealed record NormalizedLegalAuthority(
    string AuthorityIdentity,
    string SourceRef,
    string Title,
    string Passage,
    string? Jurisdiction,
    DateOnly? AuthorityDate,
    string? AuthorityKind,
    string? ProviderCode,
    string? SourceVersion,
    bool ProviderIdentityVerified,
    IReadOnlyCollection<Guid> SearchOperationIds)
{
    public Guid? NormalizedAuthorityId { get; init; }
    public Guid AtomicPropositionId { get; init; }
    public Guid LegalSearchPlanId { get; init; }
    public string PassageIdentity { get; init; } = string.Empty;
    public decimal PropositionSelectionScore { get; init; }
    public int PropositionSelectionRank { get; init; }

    public DecisionRetrievedSource ToRetrievedSource()=>new(SourceRef,Title,Passage)
    {
        SourceType=AuthorityKind?.ToUpperInvariant() switch
        {
            "CASE" or "CASE_LAW"=>EvidenceSourceType.CaseLaw,
            "STATUTE"=>EvidenceSourceType.Statute,
            "REGULATION"=>EvidenceSourceType.Regulation,
            _=>null,
        },
        Jurisdiction=Jurisdiction,
        AuthorityDate=AuthorityDate,
        SourceProvider=ProviderCode,
        SourceVersion=SourceVersion,
        ProviderIdentityVerified=ProviderIdentityVerified,
        AtomicPropositionId=AtomicPropositionId,
        LegalSearchPlanId=LegalSearchPlanId,
        NormalizedAuthorityId=NormalizedAuthorityId,
        NormalizedAuthorityIdentity=AuthorityIdentity,
        PassageIdentity=PassageIdentity,
        PropositionSelectionScore=PropositionSelectionScore,
        PropositionSelectionRank=PropositionSelectionRank,
    };
}

public sealed record LegalProviderAttempt(
    Guid LegalProviderAttemptId,
    Guid LegalSearchPlanId,
    Guid LegalSearchOperationId,
    string ProviderCode,
    LegalRetrievalOutcome Outcome,
    int RawResultCount,
    int ReturnedCount,
    string? Detail,
    long DurationMilliseconds)
{
    public LegalSearchOperationKind? OperationKind { get; init; }
    public string? Query { get; init; }
    public string RecoveryActionCode { get; init; } = "NONE";
}

public sealed record LegalAuthorityRetrievalResult(
    LegalSearchPlan Plan,
    Guid DecisionResearchNeedId,
    LegalRetrievalOutcome Outcome,
    IReadOnlyList<NormalizedLegalAuthority> Authorities,
    IReadOnlyList<LegalProviderAttempt> ProviderAttempts)
{
    public Guid LegalSearchPlanId=>Plan.LegalSearchPlanId;
}

public sealed record VerifiedLegalProposition(
    Guid DecisionResearchNeedId,
    Guid DecisionBranchId,
    string Proposition,
    NormalizedLegalAuthority Authority,
    EvidenceVerificationResult Verification);

public sealed record LegalDecisionImpactResult(
    Guid DecisionResearchNeedId,
    Guid DecisionBranchId,
    bool DecisionSignalAdmitted,
    bool DependencyStateChanged,
    bool RecompetitionTriggered,
    bool WinnerChanged,
    string OutcomeCode,
    string? Detail);

public interface ILegalResearchPlanner
{
    LegalSearchPlan Plan(LegalResearchRequest request);
}

public interface ILegalAuthorityRetrievalService
{
    Task<LegalAuthorityRetrievalResult> RetrieveAsync(
        LegalResearchRequest request,
        WideLegalGroundingConfiguration configuration,
        CancellationToken cancellationToken=default);
}
