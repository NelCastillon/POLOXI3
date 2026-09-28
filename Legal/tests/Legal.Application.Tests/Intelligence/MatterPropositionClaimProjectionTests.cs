using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Matter-proposition → ClaimProposition projection (proposition-level scoring/IV). Verifies the four
// decision-relevance signals and the verification state are derived deterministically from FactStateCode,
// Confidence, IsDecisionAuthoritative, and SUPPORTS/CONTRADICTS edge counts, and that the reused
// ClaimVerificationPrioritizer scores them on POLOXI's shared VIV scale (resolved facts → 0 IV).
public sealed class MatterPropositionClaimProjectionTests
{
    private static readonly Guid SessionId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MatterId = new("22222222-2222-2222-2222-222222222222");
    private static readonly EpistemicAuthoritySettings Settings = new();
    private static readonly ClaimVerificationPrioritizer Prioritizer = new();

    private static MatterPropositionSignalInput Signal(
        string factState,
        decimal? confidence,
        bool authoritative,
        int supports = 0,
        int contradicts = 0)
        => new(Guid.NewGuid(), MatterId, "The defendant breached the duty of care.", factState, confidence, authoritative, supports, contradicts);

    [Fact]
    public void SupportedAuthoritativeProposition_IsResolved_WithZeroInformationValue()
    {
        var claim = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Supported, 0.9m, authoritative: true, supports: 2), SessionId);

        Assert.Equal(ClaimVerificationState.Supported, claim.VerificationState);
        Assert.False(claim.IsEssential);
        Assert.Equal(0m, Prioritizer.ComputeInformationValue(claim));
    }

    [Fact]
    public void AllegedUnsupportedAuthoritativeProposition_IsEssential_AndClearsMinimumThreshold()
    {
        var claim = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Alleged, 0.2m, authoritative: true), SessionId);

        Assert.Equal(ClaimVerificationState.VerificationRequired, claim.VerificationState);
        Assert.True(claim.IsEssential);
        Assert.True(claim.Uncertainty >= 0.6m);
        Assert.True(Prioritizer.ComputeInformationValue(claim) >= Settings.MinimumVerificationIV);
    }

    [Fact]
    public void ContestedProposition_MapsToDisputed_WithHighUncertaintyAndDiscrimination()
    {
        var claim = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Supported, 0.8m, authoritative: true, supports: 1, contradicts: 1), SessionId);

        Assert.Equal(ClaimVerificationState.Disputed, claim.VerificationState);
        Assert.True(claim.Uncertainty >= 0.7m);
        Assert.Equal(0.8m, claim.Discrimination);
        Assert.True(Prioritizer.ComputeInformationValue(claim) > 0m);
    }

    [Fact]
    public void InvalidatedProposition_IsContradicted_WithZeroInformationValue()
    {
        var claim = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Invalidated, 0.5m, authoritative: false, contradicts: 1), SessionId);

        Assert.Equal(ClaimVerificationState.Contradicted, claim.VerificationState);
        Assert.Equal(0m, Prioritizer.ComputeInformationValue(claim));
    }

    [Fact]
    public void NonAuthoritativeProposition_HasLowerMaterialityThanAuthoritative()
    {
        var authoritative = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Alleged, 0.4m, authoritative: true), SessionId);
        var nonAuthoritative = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Alleged, 0.4m, authoritative: false), SessionId);

        Assert.True(authoritative.Materiality > nonAuthoritative.Materiality);
        Assert.True(authoritative.DecisionImpact > nonAuthoritative.DecisionImpact);
    }

    [Fact]
    public void MissingConfidence_UsesNeutralDefault()
    {
        var claim = MatterPropositionClaimProjection.Project(
            Signal(LegalFactStates.Alleged, confidence: null, authoritative: false), SessionId);

        // Neutral confidence 0.5 → base uncertainty 0.5, floored to 0.6 for unsupported VerificationRequired.
        Assert.True(claim.Uncertainty >= 0.5m);
    }

    [Fact]
    public void ToSignalInput_CountsSupportAndContradictEdges()
    {
        var propositionId = Guid.NewGuid();
        var proposition = new LegalEvidenceGraphPropositionDto(
            propositionId, MatterId, "Statement.", LegalFactStates.Supported, "DYNAMIC_LLM", 0.7m, true,
            new[]
            {
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, Guid.NewGuid(), LegalDocumentRelationshipTypes.Supports, null),
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, Guid.NewGuid(), LegalDocumentRelationshipTypes.Supports, null),
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, Guid.NewGuid(), LegalDocumentRelationshipTypes.Contradicts, null),
            });

        var signal = MatterPropositionClaimProjection.ToSignalInput(proposition);

        Assert.Equal(2, signal.SupportCount);
        Assert.Equal(1, signal.ContradictCount);
        Assert.Equal(propositionId, signal.PropositionId);
    }
}
