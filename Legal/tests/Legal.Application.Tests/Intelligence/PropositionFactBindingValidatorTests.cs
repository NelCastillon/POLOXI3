using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Validated Candidate Competition — proposition fact-binding validator ─────────────────────────
// Proves the deterministic guardrails from migration 0339 (mirrored in DefaultConfig) prevent a
// category-mismatched source value from ESTABLISHING an unrelated proposition, while still admitting
// legitimate bindings and preserving the supplied value as a verification obligation when rejected.
// No DB, no network, no LLM: the validator consumes only the passed-in config.
public sealed class PropositionFactBindingValidatorTests
{
    private static PropositionFactBindingValidator Validator()
        => new(PropositionFactBindingValidator.DefaultConfig());

    [Theory]
    // The Aisha Patel category mismatches: a settlement/payment status can never establish these.
    [InlineData("Settlement Status", "Disbursed", "Liability established")]
    [InlineData("Settlement Status", "Disbursed", "Damages documented")]
    [InlineData("Settlement Status", "Disbursed", "Discovery sufficient")]
    [InlineData("Settlement Status", "Disbursed", "Confidentiality enforceable")]
    public void SettlementStatus_DoesNotEstablish_UnrelatedProposition(string field, string value, string proposition)
    {
        var verdict = Validator().Validate(field, value, proposition);

        Assert.Equal(FactBindingDecision.Rejected, verdict.Decision);
        Assert.False(verdict.Establishes);
        Assert.Equal("REJECTED", verdict.Admissibility);
        // The supplied value becomes an explicit obligation, not an established fact.
        Assert.False(string.IsNullOrWhiteSpace(verdict.VerificationObligation));
    }

    [Theory]
    [InlineData("Demand Status", "Responded", "Demand accepted")]
    [InlineData("Demand Status", "Responded", "Demand rejected")]
    [InlineData("Demand Status", "Responded", "Trial readiness confirmed")]
    public void DemandStatus_Responded_DoesNotEstablish_AcceptanceRejectionOrReadiness(string field, string value, string proposition)
    {
        var verdict = Validator().Validate(field, value, proposition);

        Assert.Equal(FactBindingDecision.Rejected, verdict.Decision);
        Assert.False(verdict.Establishes);
    }

    [Theory]
    [InlineData("Liability Finding", "Admitted by defendant", "Liability established")]
    [InlineData("Damages Record", "Medical specials $84,000", "Damages quantified and proven")]
    [InlineData("Coverage Status", "Policy limits confirmed", "Coverage available applies")]
    public void LegitimateBinding_IsAdmitted_AndEstablishesProposition(string field, string value, string proposition)
    {
        var verdict = Validator().Validate(field, value, proposition);

        Assert.Equal(FactBindingDecision.Admitted, verdict.Decision);
        Assert.True(verdict.Establishes);
        Assert.Equal("ADMITTED", verdict.Admissibility);
        Assert.Null(verdict.VerificationObligation);
    }

    [Fact]
    public void UnclassifiableBinding_RequiresVerification_NotSilentlyEstablished()
    {
        // A field/proposition pair with no explicit rule and unresolvable kinds is never promoted.
        var verdict = Validator().Validate("Random Note", "some free text", "An unrelated novel proposition");

        Assert.Equal(FactBindingDecision.RequiresVerification, verdict.Decision);
        Assert.False(verdict.Establishes);
        Assert.Equal("REQUIRES_VERIFICATION", verdict.Admissibility);
        Assert.False(string.IsNullOrWhiteSpace(verdict.VerificationObligation));
    }

    [Fact]
    public void SemanticProbe_IsOffByDefault_AndNeverConsultedForExplicitDenyRules()
    {
        var probeInvoked = false;
        FactBindingSemanticProbe probe = (_, _, _) => { probeInvoked = true; return true; };

        // An explicit DENY rule wins outright; the probe must not be able to override it.
        var verdict = Validator().Validate("Settlement Status", "Disbursed", "Liability established", probe);

        Assert.Equal(FactBindingDecision.Rejected, verdict.Decision);
        Assert.False(probeInvoked);
    }

    [Fact]
    public void ClassifyField_And_ClassifyProposition_ResolveKnownKinds()
    {
        var v = Validator();
        Assert.Equal("SETTLEMENT_STATUS", v.ClassifyField("Settlement Status"));
        Assert.Equal("LIABILITY", v.ClassifyProposition("Liability established"));
        Assert.Equal(PropositionFactBindingValidator.KindUnknown, v.ClassifyField("Totally unrelated label"));
    }
}
