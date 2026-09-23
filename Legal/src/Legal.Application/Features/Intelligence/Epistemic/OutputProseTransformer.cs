namespace Legal.Application.Features.Intelligence.Epistemic;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — claim-output prose transformation (§34/§35).
//
// The disposition manifest tells the reader WHICH claims were qualified/suppressed/corrected, but on
// its own it leaves the offending prose standing verbatim in the body — so an unverified assertion can
// still be read as established fact even while the manifest disclaims it. This transformer closes that
// gap: it rewrites the prose itself so the sentence a reader sees is already reconciled with POLOXI's
// authoritative disposition, BEFORE persistence.
//
//   QUALIFY  — the claim text is rewritten in place as explicit uncertainty (not asserted as fact).
//   SUPPRESS — the claim text is removed from attorney-facing prose.
//   CORRECT  — the contradicted claim text is removed from attorney-facing prose.
//   ALLOW    — left untouched.
//
// Matching is deterministic ordinal-insensitive substring replacement. Longer claims are applied first
// so a shorter claim cannot partially rewrite text already claimed by a longer, more specific one. When
// a claim carries no text (foreign/unknown provenance) there is nothing to locate in the prose, so it
// is handled by the disposition manifest instead and skipped here.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public static class OutputProseTransformer
{
    // Authoritative outcome of a transform pass. Reports not just the rewritten text but exactly which
    // claims were located and rewritten in the prose, so callers (and the Decision Integrity Trace) can
    // tell the difference between "transformation required", "transformation attempted", and
    // "transformation actually applied". Without this, a claim whose canonical text never appears
    // verbatim in the natural-language answer silently no-ops while the trace still reports APPLIED.
    public sealed record TransformResult(
        string Text,
        IReadOnlyList<Guid> AppliedClaimIds,
        int RequiredCount,
        int AppliedCount)
    {
        // True when at least one claim required restatement but its text could not be located in the
        // prose, so an unauthorized assertion may still stand verbatim in the body.
        public bool UnauthorizedAssertionsRemain => AppliedCount < RequiredCount;
    }

    // Backward-compatible convenience overload returning only the rewritten text.
    public static string Transform(
        string? finalAnswer,
        IReadOnlyList<OutputClaimAuthorization> authorizations)
        => TransformDetailed(finalAnswer, authorizations).Text;

    public static TransformResult TransformDetailed(
        string? finalAnswer,
        IReadOnlyList<OutputClaimAuthorization> authorizations)
    {
        if (authorizations is null || authorizations.Count == 0)
            return new TransformResult(finalAnswer ?? string.Empty, [], 0, 0);

        // Claims that require restatement (non-ALLOW with locatable text). Foreign/unknown claims with no
        // text are handled by the disposition manifest and are not counted as required prose rewrites.
        var required = authorizations
            .Where(a => a.Disposition != OutputClaimDisposition.Allow
                        && !string.IsNullOrWhiteSpace(a.ClaimText))
            .ToList();

        if (string.IsNullOrWhiteSpace(finalAnswer) || required.Count == 0)
            return new TransformResult(finalAnswer ?? string.Empty, [], required.Count, 0);

        var result = finalAnswer;
        var applied = new List<Guid>();

        // Already-transformed spans are masked with unique placeholders so a later (shorter) claim can
        // never rewrite text that a longer, more specific claim already claimed. Placeholders are
        // restored verbatim at the end.
        var masks = new List<(string Token, string Value)>();

        foreach (var a in required.OrderByDescending(a => a.ClaimText.Length))
        {
            var replacement = a.Disposition switch
            {
                OutputClaimDisposition.Qualify =>
                    $"[UNRESOLVED — POLOXI has not established this; stated as uncertainty, not fact: {a.ClaimText}]",
                OutputClaimDisposition.Suppress => string.Empty,
                OutputClaimDisposition.Correct => string.Empty,
                _ => a.ClaimText,
            };

            var token = $"\uFFF9POLOXI_MASK_{masks.Count}\uFFFB";
            var replaced = ReplaceOrdinalIgnoreCase(result, a.ClaimText, token);
            if (!ReferenceEquals(replaced, result) && replaced != result)
            {
                masks.Add((token, replacement));
                result = replaced;
                applied.Add(a.ClaimId);
            }
        }

        foreach (var (token, value) in masks)
            result = result.Replace(token, value, StringComparison.Ordinal);

        return new TransformResult(result, applied, required.Count, applied.Count);
    }

    // Ordinal, case-insensitive, replace-all. Avoids regex so claim text with special characters
    // (parentheses, statute citations, quotes) is matched literally and deterministically.
    private static string ReplaceOrdinalIgnoreCase(string source, string find, string replacement)
    {
        if (string.IsNullOrEmpty(find))
            return source;

        var builder = new System.Text.StringBuilder(source.Length);
        var index = 0;
        while (true)
        {
            var match = source.IndexOf(find, index, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                builder.Append(source, index, source.Length - index);
                break;
            }

            builder.Append(source, index, match - index);
            builder.Append(replacement);
            index = match + find.Length;
        }

        return builder.ToString();
    }
}
