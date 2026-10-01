namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Decision Presentation Gate — pure, side-effect-free derivation.
//
// The authoritative POLOXI Core already decides the verdict; this policy only decides how HONESTLY that verdict
// may be presented. It maps already-persisted signals onto a single DecisionPresentationStatus so every surface
// (header, metrics, decision brief) agrees on the same epistemic truth and never calls a non-competed interpretive
// ranking a "decision". It NEVER mutates state and NEVER invents values.
//
// Authoritative inputs (all from DecisionSearchResponse):
//   • WinnerCandidateId / Candidates[].IsWinner → whether authoritative competition produced a winner
//   • ContractCompleteness                      → whether the Decision Contract is complete
//   • ReadinessVerdict.Satisfied / .Blockers    → whether decision readiness passed
//   • CandidateEntropy                          → normalized uncertainty (NOT confidence)
//   • StatusCode                                → terminal/provisional status vs DecisionStatusCodes
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
internal static class DecisionPresentationPolicy
{
    public static DecisionPresentationStatus Derive(
        DecisionSearchResponse? decision,
        DecisionCandidateDto? selected)
    {
        // No persisted decision at all → nothing may be presented as a decision.
        if (decision is null)
        {
            return new DecisionPresentationStatus(
                DecisionPresentationState.NoDecision,
                CompetitionExecuted: false,
                ContractComplete: false,
                ReadinessPassed: false,
                ContractCompletenessPct: 0,
                InterpretiveSupportPct: 0,
                NormalizedEntropyPct: 0,
                Headline: "No decision",
                BlockingReason: "No decision session has been run for this matter.",
                Blockers: []);
        }

        // Authoritative competition produced a winner only when POLOXI Core committed one AND at least two
        // candidates were competed. A normalized-but-unsupported (REGISTERED_SCORING_BLOCKED) run leaves
        // WinnerCandidateId null, so this is false even though interpretive candidates exist.
        var deliveredCandidates = decision.Candidates?.Count ?? 0;
        var hasWinner = decision.WinnerCandidateId is not null
            || (decision.Candidates?.Any(c => c.IsWinner) ?? false);
        var competitionExecuted = hasWinner && deliveredCandidates >= 2;

        var contractPct = Clamp01Pct(decision.ContractCompleteness);
        var contractComplete = decision.ContractCompleteness >= 0.999m;

        var readinessPassed = decision.ReadinessVerdict?.Satisfied == true;
        var blockers = (decision.ReadinessVerdict?.Blockers ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        var interpretivePct = selected is not null ? Clamp01Pct(selected.CompositeScore) : 0;
        var entropyPct = Clamp01Pct(decision.CandidateEntropy);

        // ── Derive the single presentation state from the gates, most-restrictive first ──
        DecisionPresentationState state;
        string headline;
        string? blockingReason;

        if (!competitionExecuted)
        {
            state = DecisionPresentationState.CompetitionBlocked;
            headline = "Registered · Competition blocked";
            blockingReason =
                "Candidate structure has been established, but authoritative candidate competition did not " +
                "produce a winner. The displayed values are interpretation-prior, not evidence-verified results.";
        }
        else if (readinessPassed && contractComplete)
        {
            state = DecisionPresentationState.DecisionReady;
            headline = "Decision ready";
            blockingReason = null;
        }
        else
        {
            state = DecisionPresentationState.Provisional;
            headline = "Provisional — competed, not yet decision-ready";
            blockingReason = !contractComplete
                ? "Candidate competition ran, but the Decision Contract is not complete."
                : "Candidate competition ran, but decision readiness is not satisfied.";
        }

        return new DecisionPresentationStatus(
            state,
            CompetitionExecuted: competitionExecuted,
            ContractComplete: contractComplete,
            ReadinessPassed: readinessPassed,
            ContractCompletenessPct: contractPct,
            InterpretiveSupportPct: interpretivePct,
            NormalizedEntropyPct: entropyPct,
            Headline: headline,
            BlockingReason: blockingReason,
            Blockers: blockers);
    }

    private static int Clamp01Pct(decimal value)
    {
        var pct = (int)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
        return Math.Clamp(pct, 0, 100);
    }
}
