using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Phase 3 immutable source anchoring: the append path must enforce the same invariants as the
// CK constraints on POLOXI.Legal_SourceAssertion (at least one anchor target, a valid forward span,
// and non-empty quote/hash provenance) before a row is ever written.
public sealed class LegalSourceAssertionPolicyTests
{
    private static readonly Guid Matter = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Version = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Passage = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Evidence = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Support = new("55555555-5555-5555-5555-555555555555");

    private static LegalSourceAssertionCreateRequest Valid(
        Guid? evidenceItemId = null,
        Guid? propositionSupportId = null,
        int startOffset = 0,
        int endOffset = 10,
        string quotedText = "the vehicle ran the red light",
        string quotedTextHash = "abc123",
        string sourceVersionHash = "def456")
        => new(Matter, Version, Passage, evidenceItemId ?? Evidence, propositionSupportId,
            startOffset, endOffset, quotedText, quotedTextHash, sourceVersionHash);

    [Fact]
    public void EvidenceAnchoredRequest_WithValidSpan_PassesValidation()
    {
        var request = Valid(evidenceItemId: Evidence);

        Assert.True(LegalSourceAssertionPolicy.HasAnchorTarget(request.LegalEvidenceItemId, request.LegalPropositionSupportId));
        Assert.True(LegalSourceAssertionPolicy.HasValidSpan(request.StartOffset, request.EndOffset));
        Assert.Null(LegalSourceAssertionPolicy.Validate(request));
    }

    [Fact]
    public void SupportOnlyAnchoredRequest_PassesValidation()
    {
        var request = new LegalSourceAssertionCreateRequest(
            Matter, Version, Passage, null, Support, 5, 25, "quote", "h1", "h2");

        Assert.True(LegalSourceAssertionPolicy.HasAnchorTarget(request.LegalEvidenceItemId, request.LegalPropositionSupportId));
        Assert.Null(LegalSourceAssertionPolicy.Validate(request));
    }

    [Fact]
    public void RequestWithNoAnchorTarget_FailsValidation()
    {
        var request = new LegalSourceAssertionCreateRequest(
            Matter, Version, Passage, null, null, 0, 10, "quote", "h1", "h2");

        Assert.False(LegalSourceAssertionPolicy.HasAnchorTarget(request.LegalEvidenceItemId, request.LegalPropositionSupportId));
        Assert.NotNull(LegalSourceAssertionPolicy.Validate(request));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 10)]
    [InlineData(10, 5)]
    public void RequestWithInvalidSpan_FailsValidation(int startOffset, int endOffset)
    {
        var request = Valid(evidenceItemId: Evidence, startOffset: startOffset, endOffset: endOffset);

        Assert.False(LegalSourceAssertionPolicy.HasValidSpan(startOffset, endOffset));
        Assert.NotNull(LegalSourceAssertionPolicy.Validate(request));
    }

    [Theory]
    [InlineData("", "hash", "hash")]
    [InlineData("quote", "", "hash")]
    [InlineData("quote", "hash", "")]
    [InlineData("   ", "hash", "hash")]
    public void RequestWithMissingProvenance_FailsValidation(string quotedText, string quotedTextHash, string sourceVersionHash)
    {
        var request = Valid(
            evidenceItemId: Evidence,
            quotedText: quotedText,
            quotedTextHash: quotedTextHash,
            sourceVersionHash: sourceVersionHash);

        Assert.NotNull(LegalSourceAssertionPolicy.Validate(request));
    }
}
