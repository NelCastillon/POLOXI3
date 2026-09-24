using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalJurisdictionScopeTests
{
    [Theory]
    [InlineData("Superior Court of California, County of Los Angeles", "California")]
    [InlineData("Supreme Court of New York", "New York")]
    [InlineData("United States District Court for the District of West Virginia", "West Virginia")]
    public void ResolveGoverningLaw_ExtractsSovereignFromCourtCaption(string caption, string expected)
    {
        Assert.Equal(expected, LegalJurisdictionScope.ResolveGoverningLaw(caption));
    }

    [Theory]
    [InlineData("California")]
    [InlineData("Delaware")]
    [InlineData("New York")]
    public void ResolveGoverningLaw_PassesBareSovereignThroughUnchanged(string sovereign)
    {
        Assert.Equal(sovereign, LegalJurisdictionScope.ResolveGoverningLaw(sovereign));
    }

    [Fact]
    public void ResolveGoverningLaw_ReturnsNullForNullOrWhitespace()
    {
        Assert.Null(LegalJurisdictionScope.ResolveGoverningLaw(null));
        Assert.Null(LegalJurisdictionScope.ResolveGoverningLaw("   "));
    }

    [Fact]
    public void ResolveGoverningLaw_KeepsCaptionWhenNoUsStatePresent()
    {
        const string caption = "High Court of Justice, Queen's Bench Division";
        Assert.Equal(caption, LegalJurisdictionScope.ResolveGoverningLaw(caption));
    }

    [Theory]
    [InlineData("Superior Court of California, County of Los Angeles", true)]
    [InlineData("California", false)]
    [InlineData("New York", false)]
    public void LooksLikeCourtCaption_DetectsForumWording(string value, bool expected)
    {
        Assert.Equal(expected, LegalJurisdictionScope.LooksLikeCourtCaption(value));
    }

    [Fact]
    public void ExtractSovereign_PrefersLongestStateMatch()
    {
        Assert.Equal("West Virginia", LegalJurisdictionScope.ExtractSovereign("Circuit Court of West Virginia"));
    }

    [Theory]
    [InlineData("United States Court of Appeals for the Ninth Circuit", "Ninth Circuit")]
    [InlineData("U.S. Court of Appeals, 9th Circuit", "Ninth Circuit")]
    [InlineData("United States Court of Appeals for the Federal Circuit", "Federal Circuit")]
    [InlineData("U.S. Supreme Court", "Supreme Court of the United States")]
    [InlineData("United States District Court", "United States")]
    public void ExtractFederalJurisdiction_TranslatesFederalCaptions(string caption, string expected)
    {
        Assert.Equal(expected, LegalJurisdictionScope.ExtractFederalJurisdiction(caption));
    }

    [Theory]
    [InlineData("Superior Court of California, County of Los Angeles")]
    [InlineData("High Court of Justice, Queen's Bench Division")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ExtractFederalJurisdiction_ReturnsNullWhenNoFederalWording(string? caption)
    {
        Assert.Null(LegalJurisdictionScope.ExtractFederalJurisdiction(caption));
    }
}
