namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Assertion → Proposition Alignment → Evidence Relation (AER) — the missing evidentiary layer.
//
// The retrieval stack answers "could this passage be relevant?" (a RetrievalRank). The light evidence
// graph and integrity gate then consume a per-binding EntailmentSatisfied signal. What has been MISSING
// is the layer that DERIVES that signal: given an exact source assertion (a quoted span) and an atomic
// decision proposition, what does the assertion actually ESTABLISH about the proposition?
//
// This layer formalizes the three concepts the blueprint says must never collapse into one score:
//   • RetrievalRank    — "could this passage be relevant?"        (0..1 similarity, carried, never reused)
//   • AlignmentResult  — "is it talking about the same thing?"    (Actor/Event/Temporal/Polarity/Condition)
//   • EvidenceRelation — "what does it actually establish?"       (DIRECT_SUPPORT/CONTRADICTS/INSUFFICIENT/…)
//
// It is a pure, side-effect-free, deterministic classifier (no DB, no LLM, no retrieval), mirroring
// LegalEvidenceAdmissionPolicy / LegalPropositionBindingPolicy / LegalLightEvidenceGraph. The dimensional
// signals it emits are cue-based and intentionally CONSERVATIVE: when the assertion does not clearly line
// up on actor, event, and polarity, the relation resolves to INSUFFICIENT rather than a wrong SUPPORT —
// consistent with the repository rule that missing/ambiguous evidence is never treated as proof. An
// LLM-derived alignment can later populate the same LegalAssertionAlignmentInput without changing callers.
//
// P32 worked example ("Defendant used his phone immediately before impact"):
//   "I used my phone immediately before impact."      → DIRECT_SUPPORT
//   "I was NOT using my phone before impact."          → CONTRADICTS
//   "I don't remember whether I used my phone."        → INSUFFICIENT
//   "My passenger was using her phone."                → WRONG_ACTOR
// ────────────────────────────────────────────────────────────────────────────────────────────────

// The five alignment dimensions the AER evaluates. Kept as codes so an LLM-backed aligner can emit them.
public static class LegalAlignmentDimensions
{
    public const string Actor = "ACTOR";
    public const string Event = "EVENT";
    public const string Temporal = "TEMPORAL";
    public const string Polarity = "POLARITY";
    public const string Condition = "CONDITION";
}

// The per-dimension outcome of comparing an assertion against a proposition.
public enum LegalDimensionAlignment
{
    // The assertion neither confirms nor conflicts with the proposition on this dimension (default).
    Unknown = 0,
    // The assertion lines up with the proposition on this dimension (same actor, same event, etc.).
    Match = 1,
    // The assertion is about a different subject on this dimension (different actor, different event, …).
    Mismatch = 2
}

// The finer evidentiary relation the AER resolves. These map onto — and never weaken — the existing
// LegalDocumentRelationshipTypes vocabulary consumed by the light graph and integrity gate.
public static class LegalEvidenceRelations
{
    // Assertion fully establishes the proposition (actor + event + polarity aligned, no blocking mismatch).
    public const string DirectSupport = "DIRECT_SUPPORT";
    // Assertion is on-point but incomplete (e.g. right actor/event but temporal/condition unresolved).
    public const string PartialSupport = "PARTIAL_SUPPORT";
    // Assertion asserts the opposite polarity of the proposition for the same actor/event.
    public const string Contradicts = "CONTRADICTS";
    // Assertion is about a different actor than the proposition — it proves nothing about this proposition.
    public const string WrongActor = "WRONG_ACTOR";
    // Assertion cannot determine the proposition either way (hedged, off-event, or too ambiguous).
    public const string Insufficient = "INSUFFICIENT";

    // Projects an AER relation onto the coarser edge vocabulary the light evidence graph / integrity
    // gate already understands, so downstream layers never have to learn the finer AER codes.
    public static string ToRelationshipTypeCode(string evidenceRelationCode)
        => evidenceRelationCode switch
        {
            DirectSupport => LegalDocumentRelationshipTypes.Supports,
            PartialSupport => LegalDocumentRelationshipTypes.Qualifies,
            Contradicts => LegalDocumentRelationshipTypes.Contradicts,
            // WRONG_ACTOR and INSUFFICIENT prove nothing → INSUFFICIENT edge (preserved for audit only).
            _ => LegalDocumentRelationshipTypes.Insufficient
        };

    // Whether this relation entails the proposition strongly enough to count as admissible support.
    // Only DIRECT_SUPPORT satisfies entailment; PARTIAL_SUPPORT is on-point but not conclusive.
    public static bool SatisfiesEntailment(string evidenceRelationCode)
        => string.Equals(evidenceRelationCode, DirectSupport, StringComparison.Ordinal);
}

// Retrieval score carried alongside the alignment, kept as a DISTINCT type so a 0.87 RetrievalRank can
// never be silently read as 87% evidentiary support. This is the "major architectural safeguard".
public readonly record struct RetrievalRank(double Value)
{
    public static RetrievalRank None => new(0d);
    public double Value { get; } = Value is >= 0d and <= 1d
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), Value, "RetrievalRank must be within [0,1].");
}

// The alignment input for one (assertion, proposition) pair. When an LLM aligner is available it can set
// the explicit per-dimension results; when it is not, the deterministic classifier infers them from the
// quoted assertion text and the proposition text using cue tokens.
public sealed record LegalAssertionAlignmentInput(
    // The exact quoted span text of the source assertion (what the document says).
    string AssertionText,
    // The atomic decision proposition text (what Judz is testing the assertion against).
    string PropositionText,
    // Optional explicit actor tokens for the proposition/assertion (e.g. "defendant"); when both are
    // supplied the actor dimension is decided by set overlap rather than shared-token inference.
    IReadOnlyCollection<string>? PropositionActors = null,
    IReadOnlyCollection<string>? AssertionActors = null,
    // The retrieval similarity that surfaced this assertion — carried, never folded into the relation.
    RetrievalRank RetrievalRank = default);

// The per-dimension alignment plus the resolved evidentiary relation. AlignmentResult ("same thing?") is
// deliberately separate from EvidenceRelationCode ("what does it establish?") and from RetrievalRank.
public sealed record LegalAssertionAlignmentResult(
    LegalDimensionAlignment Actor,
    LegalDimensionAlignment Event,
    LegalDimensionAlignment Temporal,
    LegalDimensionAlignment Polarity,
    LegalDimensionAlignment Condition,
    string EvidenceRelationCode,
    RetrievalRank RetrievalRank,
    IReadOnlyList<string> Notes)
{
    // Convenience projection to the coarse edge type the light graph / integrity gate consume.
    public string RelationshipTypeCode => LegalEvidenceRelations.ToRelationshipTypeCode(EvidenceRelationCode);
    public bool EntailmentSatisfied => LegalEvidenceRelations.SatisfiesEntailment(EvidenceRelationCode);
}

public static class LegalAssertionEvidenceRelation
{
    // Tokens indicating the assertion negates the proposition's polarity.
    private static readonly string[] NegationCues =
    [
        "not", "no ", "never", "deny", "denied", "denies", "without", "absence", "failed to",
        "did not", "wasn't", "was not", "weren't", "were not", "didn't", "isn't", "aren't", "false", "incorrect"
    ];

    // Tokens indicating the assertion is hedged / non-committal → cannot determine the proposition.
    private static readonly string[] HedgeCues =
    [
        "don't remember", "do not remember", "does not remember", "doesn't remember", "not remember",
        "not sure", "unsure", "maybe", "perhaps", "possibly", "might have", "may have",
        "cannot recall", "can't recall", "do not recall", "does not recall", "unclear", "unknown", "i think"
    ];

    // Tokens indicating a conditional / exception qualifier that leaves the proposition unresolved.
    private static readonly string[] ConditionCues =
    [
        "if ", "unless", "except", "provided that", "only when", "assuming", "subject to", "in the event"
    ];

    // Tokens indicating temporal specificity in the proposition that the assertion must also carry.
    private static readonly string[] TemporalCues =
    [
        "before", "after", "during", "immediately", "prior", "moment", "while", "when ", "at the time"
    ];

    // Resolve the full AER for one assertion/proposition pair. Pure and deterministic.
    public static LegalAssertionAlignmentResult Resolve(LegalAssertionAlignmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var notes = new List<string>();

        var propTokens = Tokenize(input.PropositionText);
        var assertionTokens = Tokenize(input.AssertionText);

        var actor = ResolveActor(input, propTokens, assertionTokens, notes);
        var @event = ResolveEvent(propTokens, assertionTokens, notes);
        var polarity = ResolvePolarity(input, notes);
        var temporal = ResolveTemporal(input, notes);
        var condition = ResolveCondition(input, notes);

        var relation = ResolveRelation(actor, @event, polarity, temporal, condition, notes);

        return new LegalAssertionAlignmentResult(
            actor, @event, temporal, polarity, condition, relation, input.RetrievalRank, notes);
    }

    // ── Dimension: ACTOR ──────────────────────────────────────────────────────────────────────────
    private static LegalDimensionAlignment ResolveActor(
        LegalAssertionAlignmentInput input,
        HashSet<string> propTokens,
        HashSet<string> assertionTokens,
        List<string> notes)
    {
        // Prefer explicit actor sets when both sides are supplied (LLM-populated path).
        if (input.PropositionActors is { Count: > 0 } propActors && input.AssertionActors is { Count: > 0 } assertionActors)
        {
            var propSet = propActors.Select(a => a.ToLowerInvariant().Trim()).ToHashSet(StringComparer.Ordinal);
            var assertionSet = assertionActors.Select(a => a.ToLowerInvariant().Trim()).ToHashSet(StringComparer.Ordinal);
            if (propSet.Overlaps(assertionSet)) return LegalDimensionAlignment.Match;
            notes.Add("ACTOR_MISMATCH");
            return LegalDimensionAlignment.Mismatch;
        }

        // Deterministic fallback: look for shared "actor-like" tokens between the two texts.
        var sharedActors = ActorTokens(propTokens).Where(ActorTokens(assertionTokens).Contains).ToList();
        if (sharedActors.Count > 0) return LegalDimensionAlignment.Match;

        // If each side names DIFFERENT actor-like tokens, treat as a mismatch (wrong-actor risk).
        var propActorTokens = ActorTokens(propTokens);
        var assertionActorTokens = ActorTokens(assertionTokens);
        if (propActorTokens.Count > 0 && assertionActorTokens.Count > 0 && !propActorTokens.Overlaps(assertionActorTokens))
        {
            notes.Add("ACTOR_MISMATCH");
            return LegalDimensionAlignment.Mismatch;
        }

        return LegalDimensionAlignment.Unknown;
    }

    // ── Dimension: EVENT ──────────────────────────────────────────────────────────────────────────
    private static LegalDimensionAlignment ResolveEvent(
        HashSet<string> propTokens,
        HashSet<string> assertionTokens,
        List<string> notes)
    {
        // Event alignment approximated by content-token overlap (excluding actor tokens): the assertion
        // must talk about the same subject matter as the proposition to bear on it.
        var propContent = propTokens.Except(ActorTokens(propTokens)).ToHashSet(StringComparer.Ordinal);
        var assertionContent = assertionTokens.Except(ActorTokens(assertionTokens)).ToHashSet(StringComparer.Ordinal);
        if (propContent.Count == 0 || assertionContent.Count == 0) return LegalDimensionAlignment.Unknown;

        var overlap = propContent.Count(assertionContent.Contains);
        var ratio = (double)overlap / propContent.Count;
        if (ratio >= 0.34) return LegalDimensionAlignment.Match;
        if (overlap == 0)
        {
            notes.Add("EVENT_OFF_TOPIC");
            return LegalDimensionAlignment.Mismatch;
        }
        return LegalDimensionAlignment.Unknown;
    }

    // ── Dimension: POLARITY ───────────────────────────────────────────────────────────────────────
    private static LegalDimensionAlignment ResolvePolarity(LegalAssertionAlignmentInput input, List<string> notes)
    {
        // A hedged assertion carries no polarity signal at all.
        if (ContainsAny(input.AssertionText, HedgeCues)) return LegalDimensionAlignment.Unknown;

        var assertionNegated = ContainsAny(input.AssertionText, NegationCues);
        var propositionNegated = ContainsAny(input.PropositionText, NegationCues);
        if (assertionNegated == propositionNegated) return LegalDimensionAlignment.Match;

        notes.Add("POLARITY_OPPOSED");
        return LegalDimensionAlignment.Mismatch;
    }

    // ── Dimension: TEMPORAL ───────────────────────────────────────────────────────────────────────
    private static LegalDimensionAlignment ResolveTemporal(LegalAssertionAlignmentInput input, List<string> notes)
    {
        // Only meaningful when the proposition itself is temporally specific.
        if (!ContainsAny(input.PropositionText, TemporalCues)) return LegalDimensionAlignment.Unknown;
        if (ContainsAny(input.AssertionText, TemporalCues)) return LegalDimensionAlignment.Match;

        notes.Add("TEMPORAL_UNRESOLVED");
        return LegalDimensionAlignment.Unknown;
    }

    // ── Dimension: CONDITION ──────────────────────────────────────────────────────────────────────
    private static LegalDimensionAlignment ResolveCondition(LegalAssertionAlignmentInput input, List<string> notes)
    {
        // A conditional/exception qualifier in the assertion leaves the proposition unresolved.
        if (ContainsAny(input.AssertionText, ConditionCues))
        {
            notes.Add("CONDITION_QUALIFIED");
            return LegalDimensionAlignment.Mismatch;
        }
        return LegalDimensionAlignment.Unknown;
    }

    // ── Relation resolver: AlignmentResult → EvidenceRelation ─────────────────────────────────────
    private static string ResolveRelation(
        LegalDimensionAlignment actor,
        LegalDimensionAlignment @event,
        LegalDimensionAlignment polarity,
        LegalDimensionAlignment temporal,
        LegalDimensionAlignment condition,
        List<string> notes)
    {
        // Wrong actor is disqualifying: the assertion is about someone else, so it proves nothing here.
        if (actor == LegalDimensionAlignment.Mismatch)
        {
            notes.Add("RELATION_WRONG_ACTOR");
            return LegalEvidenceRelations.WrongActor;
        }

        // Off-topic event → the assertion does not bear on this proposition.
        if (@event == LegalDimensionAlignment.Mismatch)
        {
            notes.Add("RELATION_INSUFFICIENT_OFF_EVENT");
            return LegalEvidenceRelations.Insufficient;
        }

        // Opposed polarity for the same actor/event → contradiction (a first-class relation).
        if (polarity == LegalDimensionAlignment.Mismatch)
        {
            notes.Add("RELATION_CONTRADICTS");
            return LegalEvidenceRelations.Contradicts;
        }

        // Must be positively on-event to support anything; otherwise we cannot determine the proposition.
        if (@event != LegalDimensionAlignment.Match || polarity != LegalDimensionAlignment.Match)
        {
            notes.Add("RELATION_INSUFFICIENT");
            return LegalEvidenceRelations.Insufficient;
        }

        // A conditional qualifier or unresolved temporal specificity keeps it short of direct support.
        if (condition == LegalDimensionAlignment.Mismatch || temporal == LegalDimensionAlignment.Unknown)
        {
            notes.Add("RELATION_PARTIAL_SUPPORT");
            return LegalEvidenceRelations.PartialSupport;
        }

        notes.Add("RELATION_DIRECT_SUPPORT");
        return LegalEvidenceRelations.DirectSupport;
    }

    // ── Cue / token helpers (mirroring LegalPropositionBindingPolicy conventions) ──────────────────
    private static bool ContainsAny(string? text, string[] cues)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lowered = " " + text.ToLowerInvariant() + " ";
        return cues.Any(cue => lowered.Contains(cue, StringComparison.Ordinal));
    }

    // A small closed set of actor-like tokens common to litigation propositions. Deterministic and
    // conservative — the explicit actor sets on the input are the authoritative path when available.
    private static readonly HashSet<string> ActorLexicon = new(StringComparer.Ordinal)
    {
        "defendant", "plaintiff", "driver", "passenger", "witness", "officer", "claimant",
        "insured", "insurer", "employer", "employee", "respondent", "petitioner", "operator"
    };

    private static HashSet<string> ActorTokens(HashSet<string> tokens)
        => tokens.Where(ActorLexicon.Contains).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return text
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', ';', ':', '(', ')', '"', '\'', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .ToHashSet(StringComparer.Ordinal);
    }
}
