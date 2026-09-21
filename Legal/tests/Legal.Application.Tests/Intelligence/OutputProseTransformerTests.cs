using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — claim-output prose transformation (§34/§35).
//
// Pins that OutputProseTransformer rewrites the answer BODY per disposition, not just the manifest:
//   • QUALIFY  — the claim sentence is rewritten in place as explicit uncertainty (text preserved).
//   • SUPPRESS — the claim sentence is replaced with a withheld-assertion marker (text removed).
//   • CORRECT  — the claim sentence is replaced with a removed/contradicted marker (text removed).
//   • ALLOW    — left untouched verbatim.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class OutputProseTransformerTests
{
    private static OutputClaimAuthorization Auth(
        string text, OutputClaimDisposition disposition) => new()
    {
        ClaimId = Guid.NewGuid(),
        ClaimText = text,
        VerificationState = ClaimVerificationState.Unverified,
        DecisionAuthority = ClaimDecisionAuthority.None,
        Disposition = disposition,
        Reason = "test",
    };

    [Fact]
    public void Qualify_RewritesClaimInPlace_AsUncertainty_PreservingText()
    {
        const string claim = "The plaintiff qualifies for the motor carrier exemption.";
        var answer = $"Analysis. {claim} Therefore judgment should enter for the defendant.";

        var result = OutputProseTransformer.Transform(answer, [Auth(claim, OutputClaimDisposition.Qualify)]);

        Assert.DoesNotContain($" {claim} ", result);
        Assert.Contains("UNRESOLVED", result);
        Assert.Contains(claim, result); // original text preserved inside the uncertainty wrapper
        Assert.Contains("Analysis.", result);
        Assert.Contains("Therefore judgment should enter for the defendant.", result);
    }

    [Fact]
    public void Suppress_RemovesClaimText_AndInsertsWithheldMarker()
    {
        const string claim = "The small-vehicle exception restores overtime eligibility.";
        var answer = $"Intro. {claim} Conclusion.";

        var result = OutputProseTransformer.Transform(answer, [Auth(claim, OutputClaimDisposition.Suppress)]);

        Assert.DoesNotContain(claim, result);
        Assert.Contains("CLAIM WITHHELD", result);
        Assert.Contains("Intro.", result);
        Assert.Contains("Conclusion.", result);
    }

    [Fact]
    public void Correct_RemovesClaimText_AndInsertsRemovedMarker()
    {
        const string claim = "The exemption clearly applies as a matter of law.";
        var answer = $"Lead. {claim} Trail.";

        var result = OutputProseTransformer.Transform(answer, [Auth(claim, OutputClaimDisposition.Correct)]);

        Assert.DoesNotContain(claim, result);
        Assert.Contains("CLAIM REMOVED", result);
    }

    [Fact]
    public void Allow_LeavesProseUntouched()
    {
        const string claim = "The vehicles weigh under 10,001 pounds.";
        var answer = $"Facts. {claim} Done.";

        var result = OutputProseTransformer.Transform(answer, [Auth(claim, OutputClaimDisposition.Allow)]);

        Assert.Equal(answer, result);
    }

    [Fact]
    public void LongerClaimAppliedFirst_ShorterDoesNotPartiallyRewrite()
    {
        const string shortClaim = "the exemption applies";
        const string longClaim = "the exemption applies only to interstate commerce";
        var answer = $"Note: {longClaim}.";

        var result = OutputProseTransformer.Transform(
            answer,
            [
                Auth(shortClaim, OutputClaimDisposition.Suppress),
                Auth(longClaim, OutputClaimDisposition.Qualify),
            ]);

        // The longer, more specific claim wins the full span; the shorter one must not have carved it up.
        Assert.Contains("UNRESOLVED", result);
        Assert.DoesNotContain("CLAIM WITHHELD", result);
        Assert.Contains(longClaim, result);
    }

    [Fact]
    public void CaseInsensitiveMatch_RewritesRegardlessOfCasing()
    {
        const string claim = "The Driver Is Exempt";
        var answer = "Ruling: the driver is exempt under the statute.";

        var result = OutputProseTransformer.Transform(answer, [Auth(claim, OutputClaimDisposition.Suppress)]);

        Assert.DoesNotContain("the driver is exempt", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CLAIM WITHHELD", result);
    }

    [Fact]
    public void ForeignClaimWithoutText_IsSkipped_LeavingProseUnchanged()
    {
        var answer = "Body remains as written.";
        var foreign = new OutputClaimAuthorization
        {
            ClaimId = Guid.NewGuid(),
            ClaimText = string.Empty,
            VerificationState = ClaimVerificationState.Unverifiable,
            DecisionAuthority = ClaimDecisionAuthority.None,
            Disposition = OutputClaimDisposition.Suppress,
            IsForeign = true,
            Reason = "foreign",
        };

        var result = OutputProseTransformer.Transform(answer, [foreign]);

        Assert.Equal(answer, result);
    }

    [Fact]
    public void EmptyAnswer_ReturnsEmpty_WithoutThrowing()
    {
        var result = OutputProseTransformer.Transform(
            null, [Auth("anything", OutputClaimDisposition.Suppress)]);

        Assert.Equal(string.Empty, result);
    }

    // ── TransformDetailed: authoritative required/applied reporting ──────────────────────────────────

    [Fact]
    public void TransformDetailed_ReportsAppliedClaim_WhenTextLocatedInProse()
    {
        const string claim = "The plaintiff qualifies for the exemption.";
        var answer = $"Analysis. {claim} Conclusion.";
        var auth = Auth(claim, OutputClaimDisposition.Qualify);

        var result = OutputProseTransformer.TransformDetailed(answer, [auth]);

        Assert.Equal(1, result.RequiredCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.False(result.UnauthorizedAssertionsRemain);
        Assert.Contains(auth.ClaimId, result.AppliedClaimIds);
        Assert.Contains("UNRESOLVED", result.Text);
    }

    [Fact]
    public void TransformDetailed_FlagsUnauthorizedRemaining_WhenClaimTextNotFoundInProse()
    {
        // This is the real-world failure: the canonical claim text never appears verbatim in the
        // natural-language answer, so the transformer cannot rewrite it. The result MUST report that a
        // required transformation was not applied (unauthorized assertion still stands) rather than
        // silently claiming success.
        const string canonicalClaim = "PLAINTIFF_QUALIFIES_MOTOR_CARRIER_EXEMPTION";
        var answer = "There are genuine disputes and the evidence supports the plaintiff.";
        var auth = Auth(canonicalClaim, OutputClaimDisposition.Qualify);

        var result = OutputProseTransformer.TransformDetailed(answer, [auth]);

        Assert.Equal(1, result.RequiredCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.True(result.UnauthorizedAssertionsRemain);
        Assert.Empty(result.AppliedClaimIds);
        Assert.Equal(answer, result.Text);
    }

    [Fact]
    public void TransformDetailed_AllowOnly_RequiresNothing()
    {
        var answer = "Facts stand as written.";
        var result = OutputProseTransformer.TransformDetailed(
            answer, [Auth("something", OutputClaimDisposition.Allow)]);

        Assert.Equal(0, result.RequiredCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.False(result.UnauthorizedAssertionsRemain);
    }
}
