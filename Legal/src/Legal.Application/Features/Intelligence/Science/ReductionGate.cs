namespace Legal.Application.Features.Intelligence.Science;

// ── Reduction / Non-circularity Gate (P1 #reduction-test) ─────────────────────────────────────────────
// A nicer representation is NOT a smaller problem. The Navier–Stokes run repeatedly relabelled the
// problem (Q → Q_i → taxonomy → axes) and called it a "genuine mathematical reduction". This gate forces
// a candidate to PROVE it actually decreases proof burden before it may be labelled GENUINE_REDUCTION:
//
//   ReductionValue(Q) = UpstreamDerivability × DownstreamPower × NonCircularity × ProofBurdenReduction
//
// Each factor is a 0..1 deterministic input the pipeline supplies. If ProofBurdenReduction is unproven
// (<= 0) or NonCircularity fails, the candidate cannot be a genuine reduction — it is at most a
// REPRESENTATION_CHANGE. This keeps "better representation ≠ smaller problem" mechanically enforced.
public sealed class ReductionGate
{
    private readonly double _genuineThreshold;

    public ReductionGate(double genuineThreshold = 0.5)
    {
        _genuineThreshold = genuineThreshold;
    }

    public ReductionAssessment Evaluate(ReductionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        var upstream = Clamp(claim.UpstreamDerivability);
        var downstream = Clamp(claim.DownstreamPower);
        var nonCircular = Clamp(claim.NonCircularity);
        var burden = Clamp(claim.ProofBurdenReduction);

        var value = upstream * downstream * nonCircular * burden;

        // Circularity or zero proof-burden reduction disqualifies a genuine reduction outright.
        var circular = nonCircular < double.Epsilon || claim.DependsOnTarget;
        var noBurdenReduction = burden < double.Epsilon;

        ReductionVerdict verdict;
        string reason;
        if (circular)
        {
            verdict = ReductionVerdict.Circular;
            reason = "Reduction is circular: the candidate depends on the target it claims to reduce.";
        }
        else if (noBurdenReduction)
        {
            verdict = ReductionVerdict.RepresentationChange;
            reason = "No proof-burden reduction demonstrated: this is a representation change, not a reduction.";
        }
        else if (value >= _genuineThreshold)
        {
            verdict = ReductionVerdict.GenuineReduction;
            reason = $"Genuine reduction: value {value:0.###} meets threshold {_genuineThreshold:0.###}.";
        }
        else
        {
            verdict = ReductionVerdict.WeakReduction;
            reason = $"Weak reduction: value {value:0.###} below threshold {_genuineThreshold:0.###}.";
        }

        return new ReductionAssessment
        {
            CandidateId = claim.CandidateId,
            ReductionValue = value,
            Verdict = verdict,
            Reason = reason,
        };
    }

    private static double Clamp(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
}

// The deterministic inputs the pipeline supplies for a reduction candidate. DependsOnTarget short-circuits
// to a circularity failure regardless of the numeric factors.
public sealed record ReductionClaim
{
    public required string CandidateId { get; init; }

    public double UpstreamDerivability { get; init; }

    public double DownstreamPower { get; init; }

    public double NonCircularity { get; init; }

    public double ProofBurdenReduction { get; init; }

    // True when the candidate's proof requires the very target it claims to reduce (circular).
    public bool DependsOnTarget { get; init; }
}

public sealed record ReductionAssessment
{
    public required string CandidateId { get; init; }

    public double ReductionValue { get; init; }

    public ReductionVerdict Verdict { get; init; }

    public required string Reason { get; init; }

    public bool IsGenuine => Verdict == ReductionVerdict.GenuineReduction;
}

public enum ReductionVerdict
{
    GenuineReduction,
    WeakReduction,
    RepresentationChange,
    Circular,
}
