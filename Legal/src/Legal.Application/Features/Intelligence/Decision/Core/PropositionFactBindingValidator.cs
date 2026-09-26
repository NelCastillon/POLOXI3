namespace Legal.Application.Features.Intelligence.Decision.Core;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Proposition-specific fact-binding validator (Validated Candidate Competition, item 1).
//
// Purpose: decide, deterministically and BEFORE any value influences candidate evaluation, whether a
// supplied source value is allowed to ESTABLISH a proposition. The prior binder used lexical token
// overlap, which let "SettlementStatus = Disbursed" populate unrelated propositions such as "Liability
// established" or "Confidentiality enforceable". This validator classifies the source field and the
// proposition into semantic KINDS (via DB-backed keyword patterns) and consults DB-backed ALLOW/DENY
// admissibility rules. A category mismatch is REJECTED: the supplied value is preserved verbatim but is
// NOT promoted to an established proposition — instead a precise verification obligation is generated.
//
// Design:
//   • Pure and deterministic. All configuration is passed in (loaded once per run from POLOXI config
//     tables), so this is directly unit-testable from fixtures with no DB/LLM dependency.
//   • Fail-soft: DefaultConfig() supplies an embedded fallback mirroring the 0339 seed so the
//     deterministic guardrails ALWAYS run even if the DB load degrades. The DB remains source of truth.
//   • The optional LLM semantic-match confirmation is injected as a delegate and is consulted ONLY for
//     bindings that survive the deterministic guardrails but remain ambiguous (no explicit rule). It is
//     off by default (gated by a disabled DB setting) so the standard path adds no new model call.
// ───────────────────────────────────────────────────────────────────────────────────────────────

// A keyword-pattern → semantic-kind classifier row (Scope = FIELD or PROPOSITION).
public sealed record FactBindingKindRule(string Scope, string KindCode, IReadOnlyList<string> Keywords, int MatchPriority);

// An admissibility rule: whether a source value of FieldKind may establish a proposition of PropositionKind.
public sealed record FactBindingAdmissibilityRule(string FieldKindCode, string PropositionKindCode, bool Allow, string? Rationale);

// The full, DB-backed configuration the validator consumes.
public sealed record FactBindingConfig(
    IReadOnlyList<FactBindingKindRule> KindRules,
    IReadOnlyList<FactBindingAdmissibilityRule> AdmissibilityRules);

// Outcome of validating a single (field-value → proposition) binding.
public enum FactBindingDecision { Admitted, Rejected, RequiresVerification }

public sealed record FactBindingVerdict(
    FactBindingDecision Decision,
    string FieldKind,
    string PropositionKind,
    string EvidenceAdmissionState,   // maps to POLOXI.Legal_EvidenceAdmissionState
    string? VerificationObligation,
    string Rationale)
{
    public string Admissibility => Decision switch
    {
        FactBindingDecision.Admitted => "ADMITTED",
        FactBindingDecision.Rejected => "REJECTED",
        _ => "REQUIRES_VERIFICATION",
    };

    public bool Establishes => Decision == FactBindingDecision.Admitted;
}

// Optional semantic-match hook: given the field label/value and the proposition, returns true when the
// value genuinely establishes the proposition. Only consulted for ambiguous survivors; fail-soft.
public delegate bool FactBindingSemanticProbe(string fieldLabel, string? fieldValue, string propositionLabel);

public sealed class PropositionFactBindingValidator
{
    public const string KindUnknown = "UNKNOWN";

    // Evidence-admission ladder codes (mirrors POLOXI.Legal_EvidenceAdmissionState).
    public const string StateSupplied = "SUPPLIED";
    public const string StateUnresolved = "UNRESOLVED";

    private static readonly char[] TokenSeparators =
        [' ', '\t', '-', '\u2014', ',', '/', '(', ')', ':', ';', '.', '\u00a7'];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","of","for","and","or","to","in","is","are","was","were","status","value",
        "any","all","with","on","by","at","this","that","its","their","established","documented",
    };

    private readonly IReadOnlyList<FactBindingKindRule> _fieldRules;
    private readonly IReadOnlyList<FactBindingKindRule> _propositionRules;
    private readonly Dictionary<(string Field, string Prop), bool> _admissibility;

    public PropositionFactBindingValidator(FactBindingConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _fieldRules = config.KindRules
            .Where(r => string.Equals(r.Scope, "FIELD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.MatchPriority).ToArray();
        _propositionRules = config.KindRules
            .Where(r => string.Equals(r.Scope, "PROPOSITION", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.MatchPriority).ToArray();
        _admissibility = new Dictionary<(string, string), bool>();
        foreach (var rule in config.AdmissibilityRules)
        {
            var key = (rule.FieldKindCode.ToUpperInvariant(), rule.PropositionKindCode.ToUpperInvariant());
            _admissibility[key] = rule.Allow; // last write wins; seed is de-duplicated by unique constraint
        }
    }

    // Classify a matter-field label into a semantic FIELD kind (UNKNOWN when nothing matches).
    public string ClassifyField(string? fieldLabel) => Classify(fieldLabel, _fieldRules);

    // Classify a proposition/factor label into a semantic PROPOSITION kind (UNKNOWN when nothing matches).
    public string ClassifyProposition(string? propositionLabel) => Classify(propositionLabel, _propositionRules);

    // Validate a single binding. Deterministic guardrails run first; the optional semantic probe is only
    // consulted for genuinely ambiguous survivors (no explicit ALLOW/DENY rule and both kinds resolvable).
    public FactBindingVerdict Validate(
        string? fieldLabel,
        string? fieldValue,
        string propositionLabel,
        FactBindingSemanticProbe? semanticProbe = null)
    {
        var fieldKind = ClassifyField(fieldLabel);
        var propKind = ClassifyProposition(propositionLabel);

        // Explicit admissibility rule wins outright.
        if (_admissibility.TryGetValue((fieldKind, propKind), out var allow))
        {
            return allow
                ? new FactBindingVerdict(FactBindingDecision.Admitted, fieldKind, propKind, StateSupplied,
                    null, $"Source kind '{fieldKind}' is admissible to proposition kind '{propKind}'.")
                : Reject(fieldKind, propKind, fieldLabel, fieldValue, propositionLabel,
                    $"Source '{Describe(fieldLabel, fieldValue)}' (kind '{fieldKind}') does not establish '{propositionLabel}' (kind '{propKind}').");
        }

        // No explicit rule. If either side is unclassifiable, the binding is ambiguous → require verification
        // (never silently established). Optionally let a semantic probe promote a clearly-matching binding.
        if (semanticProbe is not null && fieldKind != KindUnknown && propKind != KindUnknown)
        {
            try
            {
                if (semanticProbe(fieldLabel ?? string.Empty, fieldValue, propositionLabel))
                    return new FactBindingVerdict(FactBindingDecision.Admitted, fieldKind, propKind, StateSupplied,
                        null, "Semantic-match confirmation admitted the binding.");
            }
            catch
            {
                // Fail-soft: a probe failure never establishes or hard-rejects; fall through to REQUIRES_VERIFICATION.
            }
        }

        return new FactBindingVerdict(FactBindingDecision.RequiresVerification, fieldKind, propKind, StateUnresolved,
            $"Confirm that '{Describe(fieldLabel, fieldValue)}' materially supports '{propositionLabel}' before relying on it.",
            "No explicit admissibility rule; binding retained as a verification obligation, not established.");
    }

    private static FactBindingVerdict Reject(
        string fieldKind, string propKind, string? fieldLabel, string? fieldValue, string propositionLabel, string rationale)
        => new(FactBindingDecision.Rejected, fieldKind, propKind, StateUnresolved,
            $"Establish '{propositionLabel}' from an appropriate source; '{Describe(fieldLabel, fieldValue)}' does not.",
            rationale);

    private static string Describe(string? fieldLabel, string? fieldValue)
    {
        var label = string.IsNullOrWhiteSpace(fieldLabel) ? "supplied value" : fieldLabel.Trim();
        return string.IsNullOrWhiteSpace(fieldValue) ? label : $"{label} = {fieldValue!.Trim()}";
    }

    private static string Classify(string? label, IReadOnlyList<FactBindingKindRule> rules)
    {
        if (string.IsNullOrWhiteSpace(label))
            return KindUnknown;
        var tokens = Tokenize(label);
        if (tokens.Count == 0)
            return KindUnknown;

        var best = KindUnknown;
        var bestScore = 0;
        foreach (var rule in rules) // already ordered by MatchPriority desc
        {
            var overlap = rule.Keywords.Count(k => tokens.Contains(k));
            if (overlap > bestScore)
            {
                bestScore = overlap;
                best = rule.KindCode.ToUpperInvariant();
            }
        }
        return bestScore > 0 ? best : KindUnknown;
    }

    private static IReadOnlyCollection<string> Tokenize(string text)
        => text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Fail-soft embedded fallback mirroring migration 0339's seed. The DB is the source of truth; this is
    // only used when the DB-backed config could not be loaded, so the deterministic guardrails still run.
    public static FactBindingConfig DefaultConfig()
    {
        static IReadOnlyList<string> K(params string[] words) => words;

        var kinds = new List<FactBindingKindRule>
        {
            // FIELD kinds
            new("FIELD", "SETTLEMENT_STATUS", K("settlement","disbursed","disbursement","settled","payout","paid"), 200),
            new("FIELD", "DEMAND_STATUS",     K("demand","policy-limits","policy","limits","responded","response"), 200),
            new("FIELD", "PAYMENT_STATUS",    K("payment","paid","disbursed","remittance","funds","transferred"), 190),
            new("FIELD", "LIABILITY_FINDING", K("liability","fault","negligence","adjudicated","found","admitted"), 190),
            new("FIELD", "DAMAGES_RECORD",    K("damages","medical","special","economic","noneconomic","wage","loss"), 180),
            new("FIELD", "DISCOVERY_STATUS",  K("discovery","deposition","interrogatory","disclosure","production"), 180),
            new("FIELD", "CONFIDENTIALITY",   K("confidentiality","confidential","nondisclosure","nda","sealed"), 180),
            new("FIELD", "COVERAGE_STATUS",   K("coverage","policy","insurer","carrier","limits","endorsement"), 170),
            new("FIELD", "DECEDENT_IDENTITY", K("decedent","deceased","death","wrongful","survivor","heir"), 170),
            // PROPOSITION kinds
            new("PROPOSITION", "LIABILITY",        K("liability","fault","negligence","duty","breach","causation"), 200),
            new("PROPOSITION", "DAMAGES",          K("damages","quantified","proven","medical","economic"), 200),
            new("PROPOSITION", "DISCOVERY",        K("discovery","sufficient","complete","adequate","conducted"), 200),
            new("PROPOSITION", "CONFIDENTIALITY",  K("confidentiality","enforceable","agreement","provision","nondisclosure"), 200),
            new("PROPOSITION", "SETTLEMENT_TERMS", K("settlement","terms","accepted","executed","agreement","release"), 200),
            new("PROPOSITION", "DEMAND_ACCEPTED",  K("demand","accepted","acceptance","agreed"), 200),
            new("PROPOSITION", "DEMAND_REJECTED",  K("demand","rejected","declined","refused"), 200),
            new("PROPOSITION", "TRIAL_READINESS",  K("trial","ready","readiness","prepared"), 190),
            new("PROPOSITION", "COVERAGE",         K("coverage","available","applies","policy","limits","insurer"), 190),
            new("PROPOSITION", "CLAIM_VALIDITY",   K("claim","valid","viable","meritorious","cognizable"), 190),
            new("PROPOSITION", "DECEDENT_DEATH",   K("death","decedent","deceased","wrongful","died"), 190),
        };

        var rules = new List<FactBindingAdmissibilityRule>
        {
            new("SETTLEMENT_STATUS", "LIABILITY", false, "A settlement/payment status does not establish liability."),
            new("SETTLEMENT_STATUS", "DAMAGES", false, "A settlement/payment status does not document damages."),
            new("SETTLEMENT_STATUS", "DISCOVERY", false, "A settlement/payment status does not establish discovery sufficiency."),
            new("SETTLEMENT_STATUS", "CONFIDENTIALITY", false, "A settlement/payment status does not establish confidentiality enforceability."),
            new("SETTLEMENT_STATUS", "SETTLEMENT_TERMS", false, "A disbursement status does not establish specific settlement terms."),
            new("PAYMENT_STATUS", "LIABILITY", false, "Payment status does not establish liability."),
            new("PAYMENT_STATUS", "DAMAGES", false, "Payment status does not document damages."),
            new("DEMAND_STATUS", "DEMAND_ACCEPTED", false, "A demand response does not establish demand acceptance."),
            new("DEMAND_STATUS", "DEMAND_REJECTED", false, "A demand response does not establish demand rejection."),
            new("DEMAND_STATUS", "TRIAL_READINESS", false, "A demand response does not establish trial readiness."),
            new("LIABILITY_FINDING", "LIABILITY", true, "An adjudicated/admitted liability finding may establish liability."),
            new("DAMAGES_RECORD", "DAMAGES", true, "A damages record may document damages."),
            new("DISCOVERY_STATUS", "DISCOVERY", true, "A discovery status may establish discovery progress."),
            new("CONFIDENTIALITY", "CONFIDENTIALITY", true, "A confidentiality/NDA field may bear on confidentiality enforceability."),
            new("COVERAGE_STATUS", "COVERAGE", true, "A coverage status may bear on coverage availability."),
            new("DECEDENT_IDENTITY", "DECEDENT_DEATH", true, "A decedent-identity field may bear on the death element."),
        };

        return new FactBindingConfig(kinds, rules);
    }
}
