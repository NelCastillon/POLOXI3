namespace Legal.Application.Features.Intelligence.Science;

// ── Math V1 stage identifiers (additive; not yet wired) ────────────────────────────────────────────
// Stable StageCode values for the scientific-reasoning pipeline, mapping one-to-one to the MATH_* prompt
// codes. The orchestrator (IWideExecutionOrchestrator) decides which stage runs next based on
// uncertainty / proof criticality / convergence — these are just the addressable units of work.
public static class MathStageCodes
{
    public const string ProblemContract = "MATH_PROBLEM_CONTRACT";
    public const string StrategyProposal = "MATH_STRATEGY_PROPOSAL";
    public const string SolutionDerivation = "MATH_SOLUTION_DERIVATION";
    public const string StepVerification = "MATH_STEP_VERIFICATION";
    public const string CounterexampleSearch = "MATH_COUNTEREXAMPLE_SEARCH";
    public const string AnswerExtraction = "MATH_ANSWER_EXTRACTION";
    public const string SelfConsistency = "MATH_SELF_CONSISTENCY";
    public const string AnswerComposer = "MATH_ANSWER_COMPOSER";

    // Formalization Gate stage: Research -> Formalize -> Math. Converts a surviving research idea into a
    // precise Proof Contract (assumptions => claim) before the Math Solver attacks it.
    public const string FormalizationGate = "MATH_FORMALIZATION_GATE";
}
