using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic legal-context normalizer.
//
// Separates the TYPED legal-context fields the retrieval pipeline depends on:
//   - Governing substantive law (the sovereign whose law controls, e.g. "California")
//   - Court/forum (the actual court/tribunal caption, e.g. "Superior Court of California, County of
//     Los Angeles")
//
// A free-text value produced upstream frequently carries a full court caption. When that caption is
// pushed into the governing-law field, provider adapters (CourtListener) cannot resolve a
// provider-native court identifier and every retrieval strategy fails with SCOPE_UNSUPPORTED. This
// helper deterministically detects a court caption and extracts the enclosing US-state sovereign so
// the governing law resolves to the state (which providers DO support) while the caption is retained
// as the court/forum.
//
// No LLM, no prompt, no court-name aliases: only US-state name detection + caption heuristics.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public static class LegalJurisdictionScope
{
    private static readonly string[] StateNames =
    [
        "Alabama","Alaska","Arizona","Arkansas","California","Colorado","Connecticut","Delaware",
        "Florida","Georgia","Hawaii","Idaho","Illinois","Indiana","Iowa","Kansas","Kentucky",
        "Louisiana","Maine","Maryland","Massachusetts","Michigan","Minnesota","Mississippi",
        "Missouri","Montana","Nebraska","Nevada","New Hampshire","New Jersey","New Mexico",
        "New York","North Carolina","North Dakota","Ohio","Oklahoma","Oregon","Pennsylvania",
        "Rhode Island","South Carolina","South Dakota","Tennessee","Texas","Utah","Vermont",
        "Virginia","Washington","West Virginia","Wisconsin","Wyoming","District of Columbia",
    ];

    private static readonly Regex CourtCaption = new(
        @"\b(court|tribunal|forum|bench|division|county|circuit|district|superior|supreme|appellate|appeals|chancery|magistrate|judicial)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // True when the value reads like a court/forum caption rather than a bare sovereign
    // (e.g. "Superior Court of California, County of Los Angeles").
    public static bool LooksLikeCourtCaption(string? value) =>
        !string.IsNullOrWhiteSpace(value) && CourtCaption.IsMatch(value);

    // Extracts the enclosing US-state sovereign from any legal-context text. Longest match wins so
    // "West Virginia" is not shadowed by "Virginia". Returns null when no US state is present.
    public static string? ExtractSovereign(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string? best = null;
        foreach (var state in StateNames)
        {
            if (Regex.IsMatch(value, $@"\b{Regex.Escape(state)}\b", RegexOptions.IgnoreCase)
                && (best is null || state.Length > best.Length))
                best = state;
        }
        return best;
    }

    // Resolves the governing substantive law from a free-text legal-context value. When the value is
    // a court caption, the enclosing US-state sovereign is returned so downstream provider adapters
    // can translate it to a supported jurisdiction filter. Bare sovereigns (e.g. "California",
    // "Delaware") pass through unchanged, preserving existing behavior.
    public static string? ResolveGoverningLaw(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!LooksLikeCourtCaption(trimmed)) return trimmed;
        return ExtractSovereign(trimmed) ?? trimmed;
    }

    // Ordinal circuit words the US Courts of Appeals are captioned with. Index 0 is unused so the
    // array position matches the spoken ordinal (1 => First Circuit).
    private static readonly string[] CircuitOrdinals =
    [
        "", "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth",
        "Ninth", "Tenth", "Eleventh",
    ];

    private static readonly Regex NumericCircuit = new(
        @"\b(\d{1,2})(?:st|nd|rd|th)?\s+circuit\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FederalWording = new(
        @"\b(federal|united\s+states|u\.?\s*s\.?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Extracts a provider-searchable US-FEDERAL jurisdiction term from a court/forum caption when no
    // US-state sovereign applies (e.g. "United States Court of Appeals for the Ninth Circuit" =>
    // "Ninth Circuit"; "U.S. Supreme Court" => "Supreme Court of the United States"). Returns null
    // when the value carries no federal wording. Deterministic: ordinal + federal keyword detection
    // only, no court-name aliases.
    public static string? ExtractFederalJurisdiction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Regex.IsMatch(value, @"\bfederal\s+circuit\b", RegexOptions.IgnoreCase))
            return "Federal Circuit";

        var numeric = NumericCircuit.Match(value);
        if (numeric.Success
            && int.TryParse(numeric.Groups[1].Value, out var circuitNumber)
            && circuitNumber >= 1 && circuitNumber < CircuitOrdinals.Length)
            return $"{CircuitOrdinals[circuitNumber]} Circuit";

        foreach (var ordinal in CircuitOrdinals)
        {
            if (ordinal.Length == 0) continue;
            if (Regex.IsMatch(value, $@"\b{Regex.Escape(ordinal)}\s+circuit\b", RegexOptions.IgnoreCase))
                return $"{ordinal} Circuit";
        }

        if (Regex.IsMatch(value, @"\bsupreme\s+court\b", RegexOptions.IgnoreCase)
            && FederalWording.IsMatch(value))
            return "Supreme Court of the United States";

        return FederalWording.IsMatch(value) ? "United States" : null;
    }
}
