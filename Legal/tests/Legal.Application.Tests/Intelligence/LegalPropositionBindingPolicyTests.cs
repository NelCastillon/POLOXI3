using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Intake-time proposition binding/matching (blueprint §5/§12.3/§14). Verifies that a newly proposed fact
// reuses an existing canonical proposition when it confidently matches (T04 correlation), is preserved as a
// contradiction/DISPUTED without overwriting when it conflicts (T12), and becomes a NEW_ISSUE_PROPOSAL when no
// valid match exists (T18 drift / T19/T31 new distinction).
public sealed class LegalPropositionBindingPolicyTests
{
    private static readonly Guid ExistingId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void NoExistingPropositions_ProducesNewIssue()
    {
        var decision = LegalPropositionBindingPolicy.Resolve(
            "The driver used a mobile phone at the time of the collision.",
            LegalFactStates.Alleged,
            []);

        Assert.Equal(LegalPropositionBindingDisposition.NewIssue, decision.Disposition);
        Assert.Null(decision.MatchedPropositionId);
        Assert.False(decision.IsReuse);
        Assert.Null(decision.MatchedPropositionText);
    }

    [Fact]
    public void UnrelatedProposition_IsRejectedAsDriftAndBecomesNewIssue()
    {
        var existing = new[]
        {
            new LegalExistingProposition(ExistingId, "The insurance policy covered comprehensive damage.", LegalFactStates.Alleged)
        };

        var decision = LegalPropositionBindingPolicy.Resolve(
            "The medical treatment continued for several months.",
            LegalFactStates.Alleged,
            existing);

        Assert.Equal(LegalPropositionBindingDisposition.NewIssue, decision.Disposition);
        Assert.Null(decision.MatchedPropositionId);
    }

    [Fact]
    public void ConfidentAgreeingMatch_ReusesExistingPropositionWithSupportsEdge()
    {
        var existing = new[]
        {
            new LegalExistingProposition(ExistingId, "The driver used a mobile phone during the collision.", LegalFactStates.Alleged)
        };

        var decision = LegalPropositionBindingPolicy.Resolve(
            "The driver used a mobile phone during the collision sequence.",
            LegalFactStates.Alleged,
            existing);

        Assert.Equal(LegalPropositionBindingDisposition.ReuseSupport, decision.Disposition);
        Assert.Equal(ExistingId, decision.MatchedPropositionId);
        Assert.Equal(LegalDocumentRelationshipTypes.Supports, decision.RelationshipTypeCode);
        Assert.True(decision.IsReuse);
        Assert.False(decision.IsContradiction);
    }

    [Fact]
    public void ConfidentConflictingMatch_ReusesAsContradictionWithContradictsEdge()
    {
        var existing = new[]
        {
            new LegalExistingProposition(ExistingId, "The driver used a mobile phone during the collision.", LegalFactStates.Alleged)
        };

        var decision = LegalPropositionBindingPolicy.Resolve(
            "The driver did not use a mobile phone during the collision.",
            LegalFactStates.Alleged,
            existing);

        Assert.Equal(LegalPropositionBindingDisposition.ReuseContradict, decision.Disposition);
        Assert.Equal(ExistingId, decision.MatchedPropositionId);
        Assert.Equal(LegalDocumentRelationshipTypes.Contradicts, decision.RelationshipTypeCode);
        Assert.True(decision.IsContradiction);
        // The existing text is surfaced so the attorney contradiction review task can show what conflicts.
        Assert.Equal("The driver used a mobile phone during the collision.", decision.MatchedPropositionText);
    }

    [Fact]
    public void DisputedProposedState_AgainstAgreeingMatch_IsTreatedAsContradiction()
    {
        var existing = new[]
        {
            new LegalExistingProposition(ExistingId, "The driver used a mobile phone during the collision.", LegalFactStates.Alleged)
        };

        var decision = LegalPropositionBindingPolicy.Resolve(
            "The driver used a mobile phone during the collision.",
            LegalFactStates.Disputed,
            existing);

        Assert.Equal(LegalPropositionBindingDisposition.ReuseContradict, decision.Disposition);
        Assert.Equal(LegalDocumentRelationshipTypes.Contradicts, decision.RelationshipTypeCode);
    }

    [Theory]
    [InlineData(LegalFactStates.Alleged, LegalFactStates.Disputed)]
    [InlineData(LegalFactStates.Supported, LegalFactStates.Disputed)]
    [InlineData(LegalFactStates.Disputed, LegalFactStates.Disputed)]
    [InlineData(LegalFactStates.Established, LegalFactStates.Established)]
    public void ReusedFactStateCode_TransitionsToDisputedWithoutDowngradingStrongerStates(string existingState, string expected)
    {
        Assert.Equal(expected, LegalPropositionBindingPolicy.ResolveReusedFactStateCode(existingState));
    }
}
