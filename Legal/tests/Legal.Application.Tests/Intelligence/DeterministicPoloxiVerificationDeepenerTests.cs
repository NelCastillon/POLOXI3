using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Milestone C — Deterministic POLOXI deepening (investigation loop) ─────────────────────────────
// Proves the bounded deterministic deepener resolves an AMBIGUOUS decision-material semantic result
// without a model call, honoring the fail-closed rules: contradiction wins, open components/discriminators
// cap at PartiallySupported, only fully-supported-with-no-open-items yields Supported, and an outcome not
// permitted by the contract degrades to Unverifiable. No DB, no network, no LLM.
public sealed class DeterministicPoloxiVerificationDeepenerTests
{
    private static readonly IReadOnlyList<PropositionSupportState> AllOutcomes =
    [
        PropositionSupportState.Supported, PropositionSupportState.PartiallySupported,
        PropositionSupportState.Unsupported, PropositionSupportState.Contradicted,
        PropositionSupportState.Unverifiable,
    ];

    private static PoloxiVerificationContract Contract(
        IReadOnlyList<string>? unresolvedDiscriminators = null,
        IReadOnlyList<PropositionSupportState>? allowedOutcomes = null) => new()
    {
        Proposition = "Liability established",
        SourceType = EvidenceSourceType.CaseLaw,
        Passages = ["The court held the defendant liable."],
        AllowedOutcomes = allowedOutcomes ?? AllOutcomes,
        SemanticFactors = [EvidenceVerificationFactor.PropositionSupport],
        UnresolvedDiscriminators = unresolvedDiscriminators ?? [],
        MaxInputTokens = 2500,
        MaxOutputTokens = 700,
        AllowExternalRetrieval = false,
    };

    private static SemanticVerificationResult Ambiguous(
        IReadOnlyList<string> supported,
        IReadOnlyList<string> unsupported,
        IReadOnlyList<string> contradicted) => new()
    {
        PropositionSupport = new PropositionSupportResult
        {
            State = PropositionSupportState.PartiallySupported,
            Proposition = "Liability established",
            SupportedComponents = supported,
            UnsupportedComponents = unsupported,
            ContradictedComponents = contradicted,
            ReasonCode = "AMBIGUOUS",
            VerificationMethod = "SEMANTIC_LLM",
        },
        StatementRole = VerificationCheckResult.NotApplicable("N/A", "n/a"),
        Holding = VerificationCheckResult.NotApplicable("N/A", "n/a"),
        Ambiguous = true,
    };

    [Fact]
    public async Task Contradiction_ForcesContradicted_RegardlessOfSupport()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        var current = Ambiguous(supported: ["duty"], unsupported: [], contradicted: ["breach"]);

        var result = await deepener.DeepenAsync(Contract(), current);

        Assert.True(result.Deepened);
        Assert.False(result.Ambiguous);
        Assert.Equal(PropositionSupportState.Contradicted, result.PropositionSupport.State);
    }

    [Fact]
    public async Task OpenDiscriminators_CapAtPartiallySupported_WhenSomeSupportExists()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        var current = Ambiguous(supported: ["duty"], unsupported: [], contradicted: []);

        var result = await deepener.DeepenAsync(
            Contract(unresolvedDiscriminators: ["causation"]), current);

        Assert.Equal(PropositionSupportState.PartiallySupported, result.PropositionSupport.State);
    }

    [Fact]
    public async Task FullSupport_NoOpenItems_YieldsSupported()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        var current = Ambiguous(supported: ["duty", "breach"], unsupported: [], contradicted: []);

        var result = await deepener.DeepenAsync(Contract(), current);

        Assert.Equal(PropositionSupportState.Supported, result.PropositionSupport.State);
    }

    [Fact]
    public async Task NoEvidence_YieldsUnverifiable()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        var current = Ambiguous(supported: [], unsupported: [], contradicted: []);

        var result = await deepener.DeepenAsync(Contract(), current);

        Assert.Equal(PropositionSupportState.Unverifiable, result.PropositionSupport.State);
    }

    [Fact]
    public async Task ResolvedOutcomeNotAllowedByContract_DegradesToUnverifiable()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        // Evidence would justify Supported, but the contract forbids Supported.
        var current = Ambiguous(supported: ["duty", "breach"], unsupported: [], contradicted: []);
        var contract = Contract(allowedOutcomes:
        [
            PropositionSupportState.PartiallySupported, PropositionSupportState.Unverifiable,
        ]);

        var result = await deepener.DeepenAsync(contract, current);

        Assert.Equal(PropositionSupportState.Unverifiable, result.PropositionSupport.State);
    }

    [Fact]
    public async Task AlreadyResolved_IsReturnedUnchanged()
    {
        var deepener = new DeterministicPoloxiVerificationDeepener();
        var current = Ambiguous(supported: ["duty"], unsupported: [], contradicted: []) with { Ambiguous = false };

        var result = await deepener.DeepenAsync(Contract(), current);

        Assert.False(result.Deepened);
        Assert.Same(current.PropositionSupport, result.PropositionSupport);
    }
}
