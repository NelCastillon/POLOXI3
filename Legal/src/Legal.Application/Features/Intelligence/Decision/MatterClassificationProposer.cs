namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────
// 0448 Matter classification proposals. When the SAVED structured classification (subtype / PI incident
// type) resolves to a different incident family than the FACT narrative (incident/liability summaries), the
// system records a confidence-qualified PROPOSED classification for attorney decision. It never mutates the
// authoritative Legal_DecisionMatter / PersonalInjuryProfile fields: CurrentValue is a read-only snapshot.
// Confidence is derived from how decisive each side is (one family vs several) - it is advisory only.
// Decision-context fields (MotionTarget / RequestedDisposition / Posture) are reported as a DECISION_CONTEXT
// consistency note rather than a replacement proposal: a saved summary-judgment request is an authoritative
// decision-contract fact, not a classification error, and must be verified by the operator, not rewritten.
// Pure and unit-testable.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────
public static class MatterClassificationProposer
{
    public sealed record Inputs(
        string? MatterTypeCode,
        string? Subtype,
        string? ClaimTypeCode,
        string? IncidentTypeCode,
        string? MotionTarget,
        string? RequestedDisposition,
        string? Posture,
        DomainPackScopeEligibility.FamilyScope FamilyScope,
        IReadOnlyCollection<DecisionDomainPackIncidentFamilyDto> Families,
        Guid? ExecutionId);

    public static IReadOnlyList<MatterClassificationProposalEvaluation> Propose(Inputs input)
    {
        var scope = input.FamilyScope;
        if (!scope.IsConflict || scope.Narrative.Count == 0) return [];

        var narrativeFamilies = scope.Narrative.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var structuredFamilies = scope.Structured.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var proposedFamily = narrativeFamilies[0];
        var familyName = input.Families.FirstOrDefault(f => string.Equals(f.FamilyCode, proposedFamily, StringComparison.OrdinalIgnoreCase))?.Name ?? proposedFamily;

        // Decisiveness: a single narrative family against a single structured family is the clearest conflict.
        var confidence = 0.55m;
        if (narrativeFamilies.Length == 1) confidence += 0.20m;
        if (structuredFamilies.Length == 1) confidence += 0.10m;
        confidence = Math.Clamp(confidence, 0m, 0.95m);

        var structuredSignals = Truncate(string.Join(" | ", new[] { Pair("MatterType", input.MatterTypeCode), Pair("Subtype", input.Subtype), Pair("ClaimType", input.ClaimTypeCode), Pair("IncidentType", input.IncidentTypeCode) }.Where(s => s is not null)) + $" => families [{string.Join(", ", structuredFamilies)}]", 1000);
        var narrativeSignals = Truncate($"Incident/liability narrative => families [{string.Join(", ", narrativeFamilies)}]", 1000);

        var proposals = new List<MatterClassificationProposalEvaluation>(3)
        {
            new(MatterClassificationFieldCodes.IncidentFamily,
                structuredFamilies.Length == 0 ? null : string.Join(", ", structuredFamilies),
                string.Join(", ", narrativeFamilies),
                confidence, structuredSignals, narrativeSignals,
                Truncate($"{scope.ConflictReason} The narrative is fact text; the structured fields are proposed for review as '{familyName}'. Saved values are unchanged.", 2000),
                input.ExecutionId)
        };

        if (!string.IsNullOrWhiteSpace(input.Subtype))
            proposals.Add(new(MatterClassificationFieldCodes.Subtype, input.Subtype.Trim(), familyName, Math.Round(confidence * 0.9m, 4), structuredSignals, narrativeSignals,
                Truncate($"Saved subtype '{input.Subtype.Trim()}' resolves to [{string.Join(", ", structuredFamilies)}] while the incident narrative describes a '{familyName}' incident. Proposal only - attorney accepts or rejects.", 2000), input.ExecutionId));

        if (!string.IsNullOrWhiteSpace(input.IncidentTypeCode) && !scope.Narrative.Overlaps(DomainPackScopeEligibility.ResolveMatterFamilies(input.Families, [input.IncidentTypeCode])))
            proposals.Add(new(MatterClassificationFieldCodes.IncidentTypeCode, input.IncidentTypeCode.Trim(), familyName, Math.Round(confidence * 0.9m, 4), structuredSignals, narrativeSignals,
                Truncate($"Saved PI incident type '{input.IncidentTypeCode.Trim()}' does not resolve to the narrative family '{familyName}'. Proposal only - attorney accepts or rejects.", 2000), input.ExecutionId));

        return proposals;
    }

    // Decision-context consistency: reports (never rewrites) whether the saved decision contract carries a
    // procedural request the narrative alone would not predict. Returns null when nothing needs verification.
    public static MatterClassificationProposalEvaluation? DecisionContextNote(Inputs input)
    {
        var request = string.Join(" ", new[] { input.MotionTarget, input.RequestedDisposition, input.Posture }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(request) || !input.FamilyScope.IsConflict) return null;
        return new(MatterClassificationFieldCodes.DecisionContext,
            Truncate(request, 400), null, 0.50m,
            Truncate(string.Join(" | ", new[] { Pair("MotionTarget", input.MotionTarget), Pair("RequestedDisposition", input.RequestedDisposition) }.Where(s => s is not null)), 1000),
            Truncate(string.Join(", ", input.FamilyScope.Narrative.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)), 1000),
            "The saved decision contract (motion target / requested disposition / posture) is authoritative and is sent to the model unchanged. Because the matter classification is in conflict, verify that this procedural request is intended; relevance explanations referencing it are contract-driven, not hallucinated.",
            input.ExecutionId);
    }

    private static string? Pair(string key, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{key}='{value.Trim()}'";
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
