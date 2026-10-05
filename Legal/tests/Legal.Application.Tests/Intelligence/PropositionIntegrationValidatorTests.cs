using Legal.Application.Features.Intelligence.Decision.Lpi;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic PROPOSITION-INTEGRATION validation gate — §11 regression + acceptance suite.
//
// The gate is pure and DB-free: the caller supplies already-accepted sibling texts so duplication and
// stale-revision behavior are reproducible. These tests pin the structural/semantic invariants that
// protect POLOXI Core as the sole scorer:
//   • reported ≠ established fact (assertion type preserved, never promoted).
//   • negation is part of the atomic proposition text (no silent collapse into its affirmation).
//   • an exact duplicate at the same target is rejected (no double-count of the same fact).
//   • a revision/withdrawal must reference the proposition it supersedes (history, not overwrite).
//   • placementFraction is only legal between two comparable neighbors (never an inferred score).
//   • a failed check never discards the proposal — it is parked ReviewRequired / NeedsHierarchyReview.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class PropositionIntegrationValidatorTests
{
    private static RetrievedProposition Proposition(
        string text,
        LpiAssertionType assertionType = LpiAssertionType.Asserts,
        string? attributedTo = null) =>
        new(
            ProposalId: Guid.NewGuid(),
            MatterId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            SourceLocator: "p.12 ¶3",
            SourceText: text,
            PropositionText: text,
            AssertionType: assertionType,
            AttributedTo: attributedTo,
            EffectiveAt: null);

    private static LpiPlacementProposal Placement(
        LpiRelationship relationship = LpiRelationship.Supports,
        decimal? fraction = null,
        Guid? left = null,
        Guid? right = null) =>
        new(
            ProposalId: Guid.NewGuid(),
            HierarchyRevisionId: Guid.NewGuid(),
            TargetNodeId: Guid.NewGuid(),
            LeftNeighborId: left,
            RightNeighborId: right,
            PlacementFraction: fraction,
            Relationship: relationship,
            Rationale: "reviewed placement");

    private static LpiValidationInput Input(
        RetrievedProposition proposition,
        IReadOnlyList<LpiPlacementProposal>? placements = null,
        LpiOperationKind operation = LpiOperationKind.Add,
        IReadOnlyCollection<string>? acceptedTexts = null,
        Guid? supersedes = null) =>
        new(
            Proposition: proposition,
            Placements: placements ?? [Placement()],
            Operation: operation,
            TargetHierarchyRevisionCurrent: true,
            AcceptedPropositionTextsAtTargets: acceptedTexts ?? [],
            SupersedesPropositionId: supersedes);

    private static bool Failed(LpiValidationOutcome outcome, LpiValidationCheck check) =>
        outcome.Failures.Any(f => f.Check == check);

    // §11 — a reported statement is NOT promoted to an established fact: the assertion type is preserved
    // through the gate and remains valid for integration without being rewritten to Asserts.
    [Fact]
    public void Reported_Statement_PreservesAssertionType_NotPromotedToFact()
    {
        var reported = Proposition(
            "Patient reports pain radiating down the left leg.",
            LpiAssertionType.Reports,
            attributedTo: "Plaintiff");

        var outcome = PropositionIntegrationValidator.Validate(Input(reported));

        Assert.True(outcome.IsValid);
        Assert.Equal(LpiAssertionType.Reports, reported.AssertionType);
        var category = outcome.Findings.Single(f => f.Check == LpiValidationCheck.Category);
        Assert.True(category.Passed);
        Assert.Contains("Reports", category.Message, StringComparison.Ordinal);
    }

    // §11 — negation is carried verbatim inside the atomic proposition and a CONTRADICTS relationship is
    // a valid qualitative code; the gate never collapses "did not" into its affirmative.
    [Fact]
    public void Negated_Proposition_WithContradicts_IsValid()
    {
        var negated = Proposition("The defendant did not provide a warning.");
        var contradicts = new[] { Placement(LpiRelationship.Contradicts) };

        var outcome = PropositionIntegrationValidator.Validate(Input(negated, contradicts));

        Assert.True(outcome.IsValid);
        Assert.Contains("did not", negated.PropositionText, StringComparison.Ordinal);
    }

    // §11 — an exact duplicate of an already-accepted proposition at the same target is rejected so the
    // same fact cannot be double-counted. Normalization ignores case/punctuation/spacing only.
    [Fact]
    public void Duplicate_At_Same_Target_IsRejected_NoDoubleCount()
    {
        var text = "The light was red at the time of impact.";
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition(text), acceptedTexts: ["the light was red at the time of impact"]));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.Duplication));
    }

    // §11 — CONTEXT_ONLY is a valid qualitative relationship (it simply carries no support); it must not
    // be rejected by the gate. Support exclusion is enforced later by the initializer, not here.
    [Fact]
    public void ContextOnly_Relationship_IsAccepted()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("Background on the governing statute."),
                  [Placement(LpiRelationship.ContextOnly)]));

        Assert.True(outcome.IsValid);
    }

    // §11 — a compound proposition is not atomic and must be split before acceptance. The proposal is
    // preserved (ReviewRequired) rather than discarded.
    [Fact]
    public void Compound_Proposition_FailsIdentity_AndIsPreserved()
    {
        var compound = Proposition("The driver was speeding and the brakes failed.");

        var outcome = PropositionIntegrationValidator.Validate(Input(compound));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.Identity));
        Assert.Equal(LpiProposalState.ReviewRequired, outcome.PreservedState);
    }

    // §11 — no placement at all parks the proposal as NeedsHierarchyReview (never force-fit to a lexical
    // match, never silently dropped).
    [Fact]
    public void No_Placement_ParksAsNeedsHierarchyReview()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("A materially new defense is raised."), placements: []));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.Placement));
        Assert.Equal(LpiProposalState.NeedsHierarchyReview, outcome.PreservedState);
    }

    // §11 — a placementFraction without BOTH comparable neighbors is an inferred score and is rejected at
    // the AuthorityBoundary; POLOXI Core alone owns scoring.
    [Fact]
    public void PlacementFraction_Without_Neighbors_FailsAuthorityBoundary()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("An interpolated position is proposed."),
                  [Placement(fraction: 0.5m)]));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.AuthorityBoundary));
    }

    // §11 — a reviewed interpolation WITH both neighbors and an in-range fraction is legitimate.
    [Fact]
    public void PlacementFraction_With_BothNeighbors_IsValid()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("A reviewed interpolation between two comparable nodes."),
                  [Placement(fraction: 0.5m, left: Guid.NewGuid(), right: Guid.NewGuid())]));

        Assert.True(outcome.IsValid);
    }

    // §11 — a revision must reference the proposition it supersedes; without it the Revision check fails
    // and the prior wording is never overwritten.
    [Fact]
    public void Revise_Without_Supersedes_FailsRevision()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("Corrected amount is $42,000."), operation: LpiOperationKind.Revise));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.Revision));
    }

    [Fact]
    public void Revise_With_Supersedes_IsValid()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("Corrected amount is $42,000."),
                  operation: LpiOperationKind.Revise,
                  supersedes: Guid.NewGuid()));

        Assert.True(outcome.IsValid);
    }

    // §11 — a withdrawal needs only a supersession target and source identity; no placement/relationship
    // is required, and it is distinct from deletion (the record is superseded, not erased).
    [Fact]
    public void Withdraw_With_Supersedes_IsValid_NoPlacementRequired()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("This proposition is withdrawn."),
                  placements: [],
                  operation: LpiOperationKind.Withdraw,
                  supersedes: Guid.NewGuid()));

        Assert.True(outcome.IsValid);
    }

    [Fact]
    public void Withdraw_Without_Supersedes_FailsRevision()
    {
        var outcome = PropositionIntegrationValidator.Validate(
            Input(Proposition("This proposition is withdrawn."),
                  placements: [],
                  operation: LpiOperationKind.Withdraw));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.Revision));
    }

    // §11 — missing source provenance (fabricated proposition) is rejected: a proposition must cite its
    // exact source.
    [Fact]
    public void Missing_Source_FailsSourceFidelity()
    {
        var noSource = new RetrievedProposition(
            ProposalId: Guid.NewGuid(),
            MatterId: Guid.NewGuid(),
            DocumentVersionId: Guid.Empty,
            SourceLocator: "",
            SourceText: "",
            PropositionText: "A claim with no citation.",
            AssertionType: LpiAssertionType.Asserts,
            AttributedTo: null,
            EffectiveAt: null);

        var outcome = PropositionIntegrationValidator.Validate(Input(noSource));

        Assert.False(outcome.IsValid);
        Assert.True(Failed(outcome, LpiValidationCheck.SourceFidelity));
    }
}
