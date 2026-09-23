namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-2.5 canonical enum ↔ persistence-code mapping.
//
// A single, shared place that formats enums to their stored string codes and parses them back. Both
// the writer (upsert) and the readers (rehydrate) route through here so codes never drift (casing,
// spelling) between persistence and domain. Parsing is tolerant of case and falls back to the
// conservative default (e.g. unknown verification state → Proposed, unknown authority → None).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class ClaimCodes
{
    public static string ToCode(ClaimType value) => value.ToString();
    public static string ToCode(ClaimOrigin value) => value.ToString();
    public static string ToCode(ClaimVerificationState value) => value.ToString();
    public static string ToCode(ClaimDecisionAuthority value) => value.ToString();
    public static string ToCode(ClaimSupportRelationship value) => value.ToString();

    public static ClaimType ParseClaimType(string? code) =>
        Enum.TryParse<ClaimType>(code, ignoreCase: true, out var v) ? v : ClaimType.Other;

    public static ClaimOrigin ParseClaimOrigin(string? code) =>
        Enum.TryParse<ClaimOrigin>(code, ignoreCase: true, out var v) ? v : ClaimOrigin.LlmGenerated;

    public static ClaimVerificationState ParseVerificationState(string? code) =>
        Enum.TryParse<ClaimVerificationState>(code, ignoreCase: true, out var v) ? v : ClaimVerificationState.Proposed;

    public static ClaimDecisionAuthority ParseDecisionAuthority(string? code) =>
        Enum.TryParse<ClaimDecisionAuthority>(code, ignoreCase: true, out var v) ? v : ClaimDecisionAuthority.None;

    public static ClaimSupportRelationship ParseSupportRelationship(string? code) =>
        Enum.TryParse<ClaimSupportRelationship>(code, ignoreCase: true, out var v) ? v : ClaimSupportRelationship.Supports;
}
