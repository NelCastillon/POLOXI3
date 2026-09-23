using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — claim-level output authorization (§34, §35).
//
// Pins the core disposition rule OutputDisposition(c):
//   • ALLOW    — authorized (Limited/Full) AND supported.
//   • QUALIFY  — legitimate but unresolved/partial (proposed/required/in-progress/disputed/unverified);
//                transform into uncertainty rather than suppress. Absence of support ≠ evidence of falsity.
//   • SUPPRESS — unsupported/unverifiable material assertion (and foreign material handled by the auditor).
//   • CORRECT  — contradicted by authoritative state (regardless of authority).
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class OutputClaimDispositionTests
{
    [Theory]
    [InlineData(ClaimVerificationState.Supported, ClaimDecisionAuthority.Full, OutputClaimDisposition.Allow)]
    [InlineData(ClaimVerificationState.Supported, ClaimDecisionAuthority.Limited, OutputClaimDisposition.Allow)]
    // Supported but unauthorized (None) is not established output → qualify, not allow.
    [InlineData(ClaimVerificationState.Supported, ClaimDecisionAuthority.None, OutputClaimDisposition.Suppress)]
    public void Classify_AuthorizedAndSupported_IsAllow(
        ClaimVerificationState state, ClaimDecisionAuthority authority, OutputClaimDisposition expected)
    {
        Assert.Equal(expected, OutputClaimDispositions.Classify(state, authority));
    }

    [Theory]
    [InlineData(ClaimVerificationState.Proposed)]
    [InlineData(ClaimVerificationState.VerificationRequired)]
    [InlineData(ClaimVerificationState.VerificationInProgress)]
    [InlineData(ClaimVerificationState.Disputed)]
    [InlineData(ClaimVerificationState.Unverified)]
    public void Classify_UnresolvedButLegitimate_IsQualify(ClaimVerificationState state)
    {
        // Unresolved claims must be transformed into uncertainty, not suppressed.
        Assert.Equal(OutputClaimDisposition.Qualify, OutputClaimDispositions.Classify(state, ClaimDecisionAuthority.None));
    }

    [Theory]
    [InlineData(ClaimDecisionAuthority.None)]
    [InlineData(ClaimDecisionAuthority.Limited)]
    [InlineData(ClaimDecisionAuthority.Full)]
    public void Classify_Contradicted_IsCorrect_RegardlessOfAuthority(ClaimDecisionAuthority authority)
    {
        Assert.Equal(OutputClaimDisposition.Correct, OutputClaimDispositions.Classify(ClaimVerificationState.Contradicted, authority));
    }

    [Fact]
    public void Classify_Unverifiable_IsSuppress()
    {
        Assert.Equal(OutputClaimDisposition.Suppress, OutputClaimDispositions.Classify(ClaimVerificationState.Unverifiable, ClaimDecisionAuthority.None));
    }

    [Theory]
    [InlineData(OutputClaimDisposition.Allow, "ALLOW")]
    [InlineData(OutputClaimDisposition.Qualify, "QUALIFY")]
    [InlineData(OutputClaimDisposition.Suppress, "SUPPRESS")]
    [InlineData(OutputClaimDisposition.Correct, "CORRECT")]
    public void ToCode_MapsEachDisposition(OutputClaimDisposition disposition, string expected)
    {
        Assert.Equal(expected, OutputClaimDispositions.ToCode(disposition));
    }
}
