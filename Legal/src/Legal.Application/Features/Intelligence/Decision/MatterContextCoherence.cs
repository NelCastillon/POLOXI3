namespace Legal.Application.Features.Intelligence.Decision;

// 0446: advisory coherence check over the SAVED matter posture. The saved matter is the authoritative
// context the L2 relevance review receives verbatim, so an incoherent record (e.g. a Personal Injury matter
// saved with Subtype "Copyright" and MotionTarget "the class certification") contaminates every model
// explanation in the run. This helper is PURE and NON-MUTATING: it never rewrites the matter; it only
// returns warnings the run logs so the operator can repair the record. Procedural postures, matter types and
// subtypes are compared on the saved text only (no inference).
public static class MatterContextCoherence
{
    public sealed record Input(string? MatterTypeCode, string? Subtype, string? MotionTarget, string? RequestedDisposition, string? Posture);

    private static readonly string[] PersonalInjuryTypes = { "personal injury", "motor vehicle", "premises liability", "product liability", "wrongful death", "medical malpractice" };
    private static readonly string[] NonPiSubtypes = { "copyright", "patent", "trademark", "intellectual property", "securities", "antitrust", "tax", "bankruptcy", "immigration" };
    private static readonly string[] ClassActionPostures = { "class certification", "class action", "rule 23" };

    public static IReadOnlyList<string> Evaluate(Input input)
    {
        var warnings = new List<string>();
        var type = (input.MatterTypeCode ?? string.Empty).Trim();
        if (type.Length == 0) return warnings;
        var isPersonalInjury = PersonalInjuryTypes.Any(t => type.Contains(t, StringComparison.OrdinalIgnoreCase));
        if (!isPersonalInjury) return warnings;

        if (!string.IsNullOrWhiteSpace(input.Subtype) && NonPiSubtypes.Any(s => input.Subtype.Contains(s, StringComparison.OrdinalIgnoreCase)))
            warnings.Add($"SUBTYPE_INCOHERENT: matter type '{type}' saved with subtype '{input.Subtype.Trim()}'.");

        foreach (var (label, value) in new[] { ("MotionTarget", input.MotionTarget), ("Posture", input.Posture), ("RequestedDisposition", input.RequestedDisposition) })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (ClassActionPostures.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase)))
                warnings.Add($"POSTURE_INCOHERENT: matter type '{type}' saved with {label} '{value.Trim()}' (class-action posture on an individual injury matter).");
        }
        return warnings;
    }
}
