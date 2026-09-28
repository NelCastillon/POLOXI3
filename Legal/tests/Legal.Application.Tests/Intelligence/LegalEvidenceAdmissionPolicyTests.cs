using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// T11 span-presence enforcement: only evidence anchored to a passage with a traceable source span may be
// admitted as decision evidence. Span-less (or passage-less) items are downgraded to a non-admitted state and
// their support edges become INSUFFICIENT so they can never establish a proposition.
public sealed class LegalEvidenceAdmissionPolicyTests
{
    [Fact]
    public void SpanBackedPassage_IsAdmittedAsProposedEvidence()
    {
        var spanned = new Guid("11111111-1111-1111-1111-111111111111");
        var set = new HashSet<Guid> { spanned };

        Assert.True(LegalEvidenceAdmissionPolicy.IsAdmissible(spanned, set));
        Assert.Equal(LegalEvidenceStates.Proposed, LegalEvidenceAdmissionPolicy.ResolveEvidenceStateCode(spanned, set));
    }

    [Fact]
    public void SpanLessPassage_IsNotAdmitted_AndIsInvalidated()
    {
        var spanLess = new Guid("22222222-2222-2222-2222-222222222222");
        var set = new HashSet<Guid> { new("11111111-1111-1111-1111-111111111111") };

        Assert.False(LegalEvidenceAdmissionPolicy.IsAdmissible(spanLess, set));
        Assert.Equal(LegalEvidenceStates.Invalidated, LegalEvidenceAdmissionPolicy.ResolveEvidenceStateCode(spanLess, set));
    }

    [Fact]
    public void PassagelessEvidence_IsNotAdmitted()
    {
        var set = new HashSet<Guid> { new("11111111-1111-1111-1111-111111111111") };

        Assert.False(LegalEvidenceAdmissionPolicy.IsAdmissible(null, set));
        Assert.Equal(LegalEvidenceStates.Invalidated, LegalEvidenceAdmissionPolicy.ResolveEvidenceStateCode(null, set));
    }

    [Fact]
    public void AdmittedEvidence_PreservesProposedRelationshipAndRationale()
    {
        var type = LegalEvidenceAdmissionPolicy.ResolveRelationshipTypeCode(true, LegalDocumentRelationshipTypes.Supports);
        var rationale = LegalEvidenceAdmissionPolicy.ResolveRelationshipRationale(true, "Direct source-linked observation.");

        Assert.Equal(LegalDocumentRelationshipTypes.Supports, type);
        Assert.Equal("Direct source-linked observation.", rationale);
    }

    [Fact]
    public void NonAdmittedEvidence_DowngradesRelationshipToInsufficientWithDisposition()
    {
        var type = LegalEvidenceAdmissionPolicy.ResolveRelationshipTypeCode(false, LegalDocumentRelationshipTypes.Supports);
        var rationale = LegalEvidenceAdmissionPolicy.ResolveRelationshipRationale(false, "Direct source-linked observation.");

        Assert.Equal(LegalDocumentRelationshipTypes.Insufficient, type);
        Assert.Equal(LegalEvidenceAdmissionPolicy.NonAdmittedRationale, rationale);
    }

    // Phase 2 promotion gate: non-admitted (INVALIDATED) evidence must never be a promotable verification candidate.
    [Theory]
    [InlineData(LegalEvidenceStates.Proposed, true)]
    [InlineData(LegalEvidenceStates.Verified, true)]
    [InlineData(LegalEvidenceStates.Disputed, true)]
    [InlineData("proposed", true)]
    [InlineData(LegalEvidenceStates.Invalidated, false)]
    [InlineData("invalidated", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void CanBePromoted_BlocksNonAdmittedEvidence(string? evidenceStateCode, bool expected)
    {
        Assert.Equal(expected, LegalEvidenceAdmissionPolicy.CanBePromoted(evidenceStateCode));
    }
}
