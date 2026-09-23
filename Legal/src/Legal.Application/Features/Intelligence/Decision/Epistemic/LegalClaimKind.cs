namespace Legal.Application.Features.Intelligence.Decision.Epistemic;

using Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — Legal Domain Pack specialization (§5, §20).
//
// These richer legal semantics deliberately live OUTSIDE the domain-neutral core Epistemic layer.
// A legal authority is verified across SEPARATE facets — identity ≠ holding ≠ weight ≠ proposition
// support ≠ good-law — so "citation exists" can never silently become "candidate wins".
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public enum LegalClaimKind
{
    FactProposition,
    EvidenceProposition,
    AuthorityIdentity,
    AuthorityHolding,
    AuthorityWeight,
    LegalProposition,
    StatutoryText,
    RegulatoryText,
    ContractualText,
    ProceduralFact,
    BurdenRule,
    LegalInference,
    OutcomeInference
}

// Independent verification facets for a cited legal authority (§20). Each is verified separately;
// one being true does not imply the next.
public sealed record LegalAuthorityVerification
{
    public bool AuthorityIdentityVerified { get; init; }
    public bool HoldingVerified { get; init; }
    public bool AuthorityWeightVerified { get; init; }
    public bool PropositionSupportVerified { get; init; }
    public bool GoodLawVerified { get; init; }

    // Proposition support requires every prior facet; this is the only facet that may feed candidate
    // support, and even then POLOXI competition — not this method — decides who wins.
    public bool SupportsProposition() =>
        AuthorityIdentityVerified && HoldingVerified && AuthorityWeightVerified && PropositionSupportVerified;
}

// Maps a domain-neutral ClaimType into legal semantics for presentation/verification routing.
public static class LegalClaimKindMap
{
    public static LegalClaimKind From(ClaimType claimType) => claimType switch
    {
        ClaimType.Factual => LegalClaimKind.FactProposition,
        ClaimType.Numeric => LegalClaimKind.FactProposition,
        ClaimType.Temporal => LegalClaimKind.ProceduralFact,
        ClaimType.Attribution => LegalClaimKind.AuthorityIdentity,
        ClaimType.Quotation => LegalClaimKind.StatutoryText,
        ClaimType.SourceIdentity => LegalClaimKind.AuthorityIdentity,
        ClaimType.SourceContent => LegalClaimKind.AuthorityHolding,
        ClaimType.Causal => LegalClaimKind.LegalInference,
        ClaimType.Interpretive => LegalClaimKind.LegalProposition,
        ClaimType.Inferential => LegalClaimKind.LegalInference,
        ClaimType.Predictive => LegalClaimKind.OutcomeInference,
        ClaimType.Procedural => LegalClaimKind.ProceduralFact,
        _ => LegalClaimKind.LegalProposition,
    };
}
