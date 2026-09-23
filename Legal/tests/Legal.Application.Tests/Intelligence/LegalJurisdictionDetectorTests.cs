using Legal.Infrastructure.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalJurisdictionDetectorTests
{
    [Fact]
    public void TryDetect_ExplicitJurisdiction_OverridesCitationAbbreviation()
    {
        var sut=new LegalJurisdictionDetector();

        var detected=sut.TryDetect("8 Del. C. § 141 jurisdiction Delaware",out var citation);

        Assert.True(detected);
        Assert.Equal("NAME:DELAWARE",citation.JurisdictionCode);
        Assert.Equal("STATUTE",citation.AuthorityKindCode);
        Assert.Equal("8 Del. C. § 141",citation.CitationText);
    }

    [Fact]
    public void TryDetect_GeneratedQuery_DoesNotIncludeLeadingInstructionInJurisdiction()
    {
        var sut=new LegalJurisdictionDetector();

        var detected=sut.TryDetect("Find controlling authority for Delaware Code section 141 jurisdiction Delaware",out var citation);

        Assert.True(detected);
        Assert.Equal("NAME:DELAWARE",citation.JurisdictionCode);
        Assert.Equal("Delaware Code section 141",citation.CitationText);
    }

    [Fact]
    public void TryDetect_MultipleCitations_IsRejectedAsAmbiguous()
    {
        var sut=new LegalJurisdictionDetector();

        var detected=sut.TryDetect("Delaware Code section 141 and Delaware Code section 144",out _);

        Assert.False(detected);
    }
}
