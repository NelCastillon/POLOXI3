using System.Text;

namespace Legal.Application.Features.Intelligence.Decision;

// ──────────────────────────────────────────────────────────────────────────────────────────────
// R2 — Immutable Matter Context Snapshot.
//
// A single, deterministic, provenance-tagged projection of the SELECTED Matter (the shared
// DecisionMatter aggregate + its Personal Injury profile) built once by MatterId. It is a
// first-class INPUT to the Domain Pack–informed proposal process — never a replacement for the
// original user question, and never a source of verified evidence.
//
// Design rules encoded here (see copilot instructions / R2 spec):
//   • Every legal-scope field stays DISTINCT. GoverningLaw is NOT copied into Jurisdiction; the
//     saved Jurisdiction is an additional reference; IncidentState is NOT a forum identifier.
//   • A saved court description is a SUPPLIED value pending validation — never promoted to a
//     confirmed/native forum identifier by this snapshot.
//   • Every material field carries a provenance status so downstream stages can tell a supplied
//     allegation (e.g. LiabilitySummary) apart from independently verified proof.
//   • The snapshot itself performs NO inference and NO cross-field derivation.
// ──────────────────────────────────────────────────────────────────────────────────────────────

// Field-level provenance for every material Matter input (R2 §5).
public enum MatterFieldProvenance
{
    // Not established / not present.
    Unknown = 0,
    // Saved in the Matter or entered by the user (default for stored fields).
    Supplied = 1,
    // Independently supported by an appropriate source (only Core may promote to this).
    Verified = 2,
    // Conflicting assertions or evidence exist.
    Disputed = 3,
    // Proposed interpretation, not an established fact.
    Inferred = 4,
}

// A single provenance-tagged field in the snapshot. Keeps identity (label), value, and status
// together so distinct legal fields are never collapsed into one another during projection.
public sealed record MatterContextField(string Label, string? Value, MatterFieldProvenance Provenance)
{
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);
}

// The immutable snapshot. Groups mirror R2 §1: Decision, Legal scope, PI profile, Facts, Evidence.
public sealed record MatterContextSnapshot(
    Guid MatterId,
    Guid TenantId,
    // The original user question is preserved SEPARATELY and verbatim; it is never merged into
    // the structured groups and never suppressed by them.
    string OriginalQuestion,
    string? DomainPackCode,
    string? PracticeAreaCode,
    IReadOnlyList<MatterContextField> Decision,
    IReadOnlyList<MatterContextField> LegalScope,
    IReadOnlyList<MatterContextField> PersonalInjuryProfile,
    IReadOnlyList<MatterContextField> Facts,
    IReadOnlyList<MatterContextField> Evidence)
{
    public static readonly MatterContextSnapshot Empty = new(
        Guid.Empty, Guid.Empty, string.Empty, null, null, [], [], [], [], []);

    public bool HasAnyContext =>
        Decision.Any(f => f.HasValue) || LegalScope.Any(f => f.HasValue) ||
        PersonalInjuryProfile.Any(f => f.HasValue) || Facts.Any(f => f.HasValue) ||
        Evidence.Any(f => f.HasValue);

    // Total number of populated fields across all groups — used for DEV diagnostics/logging so we can
    // confirm how many saved matter fields actually reach the query contract.
    public int FieldCount =>
        Decision.Count(f => f.HasValue) + LegalScope.Count(f => f.HasValue) +
        PersonalInjuryProfile.Count(f => f.HasValue) + Facts.Count(f => f.HasValue) +
        Evidence.Count(f => f.HasValue);

    // Deterministic, human/LLM-readable serialization used both for prompt projection and DEV
    // diagnostics. Groups render in a fixed order; empty fields are omitted so the payload stays
    // bounded, but each legal field keeps its distinct label + provenance tag.
    public string ToPromptBlock()
    {
        var sb = new StringBuilder();
        // Domain Pack / Practice Area are first-class routing context — surface them so the discovery
        // and graph-proposal prompts anchor on the correct legal domain, not a generic answer path.
        if (!string.IsNullOrWhiteSpace(DomainPackCode) || !string.IsNullOrWhiteSpace(PracticeAreaCode))
        {
            sb.Append("MATTER DOMAIN:").Append('\n');
            if (!string.IsNullOrWhiteSpace(DomainPackCode))
                sb.Append("  Domain Pack: ").Append(DomainPackCode).Append("  [SUPPLIED]").Append('\n');
            if (!string.IsNullOrWhiteSpace(PracticeAreaCode))
                sb.Append("  Practice Area: ").Append(PracticeAreaCode).Append("  [SUPPLIED]").Append('\n');
        }
        AppendGroup(sb, "MATTER DECISION", Decision);
        AppendGroup(sb, "MATTER LEGAL CONTEXT", LegalScope);
        AppendGroup(sb, "PERSONAL INJURY CONTEXT", PersonalInjuryProfile);
        AppendGroup(sb, "FACT AND EVIDENCE STATUS", Facts);
        AppendGroup(sb, "AVAILABLE EVIDENCE", Evidence);
        return sb.ToString().TrimEnd();
    }

    private static void AppendGroup(StringBuilder sb, string header, IReadOnlyList<MatterContextField> fields)
    {
        var present = fields.Where(f => f.HasValue).ToList();
        if (present.Count == 0)
            return;
        sb.Append(header).Append(':').Append('\n');
        foreach (var field in present)
            sb.Append("  ").Append(field.Label).Append(": ").Append(field.Value)
              .Append("  [").Append(field.Provenance.ToString().ToUpperInvariant()).Append(']').Append('\n');
    }
}

// Deterministic builder that projects the raw Matter + PI DTOs into the immutable snapshot.
// Performs NO inference and NO cross-field derivation — it only maps saved values to their
// distinct labels and provenance. All summary allegations (Liability/Injury/Damages) are marked
// SUPPLIED, never VERIFIED, so the Light Evidence Graph does not treat them as proof.
public static class MatterContextSnapshotBuilder
{
    public static MatterContextSnapshot Build(
        Guid tenantId,
        string originalQuestion,
        DecisionMatterDto matter,
        PersonalInjuryProfileDto? profile)
    {
        ArgumentNullException.ThrowIfNull(matter);

        var decision = new List<MatterContextField>
        {
            Supplied("Original Question", originalQuestion),
            Supplied("Matter Title", matter.Title),
            Supplied("Decision Type", matter.MatterTypeCode),
            Supplied("Subtype", matter.Subtype),
            Supplied("Requested Disposition", matter.RequestedDisposition),
            Supplied("Current Outcome", matter.CurrentOutcome),
            Supplied("Posture", matter.Posture),
            Supplied("Description", matter.Description),
        };

        // Legal scope — every field kept DISTINCT. No cross-derivation.
        var legalScope = new List<MatterContextField>
        {
            Supplied("Governing Law", matter.GoverningLaw),
            // Saved Jurisdiction is an additional reference, NOT GoverningLaw and NOT a forum id.
            Supplied("Jurisdiction (saved reference)", matter.Jurisdiction),
            Supplied("State", matter.State),
            // Court fields are SUPPLIED values pending validation — never a confirmed native forum.
            Supplied("Court System (supplied, pending validation)", matter.CourtSystem),
            Supplied("Court Level (supplied, pending validation)", matter.CourtLevel),
            Supplied("County (supplied, pending validation)", matter.County),
            Supplied("Subject-Matter Jurisdiction", matter.SubjectMatterJurisdiction),
            Supplied("Personal/Territorial Jurisdiction", matter.PersonalTerritorialJurisdiction),
            Supplied("Procedural Law", matter.ProceduralLaw),
            Supplied("Authority Cutoff Date", matter.AuthorityCutoffDate?.ToString("yyyy-MM-dd")),
        };

        var piProfile = new List<MatterContextField>();
        var facts = new List<MatterContextField>();
        var evidence = new List<MatterContextField>();

        if (profile is not null)
        {
            piProfile.Add(Supplied("Incident Type", profile.IncidentTypeCode));
            piProfile.Add(Supplied("Incident Date", profile.IncidentDate?.ToString("yyyy-MM-dd")));
            // IncidentState is the LOCATION of the incident, NOT a forum and NOT GoverningLaw.
            piProfile.Add(Supplied("Incident State (location, not forum)", profile.IncidentState));
            piProfile.Add(Supplied("Incident County", profile.IncidentCounty));
            piProfile.Add(Supplied("Incident City", profile.IncidentCity));
            piProfile.Add(Supplied("Current Stage", profile.CurrentStageCode));
            piProfile.Add(Supplied("Litigation Status", profile.LitigationStatusCode));
            piProfile.Add(Supplied("Demand Status", profile.DemandStatusCode));
            piProfile.Add(Supplied("Settlement Status", profile.SettlementStatusCode));

            // Summaries are ALLEGATIONS: supplied, never verified. Facts group carries provenance.
            facts.Add(Supplied("Incident Summary (alleged)", profile.IncidentSummary));
            facts.Add(Supplied("Liability Summary (alleged)", profile.LiabilitySummary));
            facts.Add(Supplied("Injury Summary (alleged)", profile.InjurySummary));
            facts.Add(Supplied("Treatment Summary (alleged)", profile.TreatmentSummary));
            facts.Add(Supplied("Damages Summary (alleged)", profile.DamagesSummary));

            // Evidence inventory — counts of structured child records available for research/verify.
            AddCount(evidence, "Insurance Policies on file", profile.InsurancePolicies.Count);
            AddCount(evidence, "Documented Injuries", profile.Injuries.Count);
            AddCount(evidence, "Treatment Records", profile.Treatments.Count);
            AddCount(evidence, "Medical Bills", profile.MedicalBills.Count);
            AddCount(evidence, "Documented Damages", profile.Damages.Count);
            AddCount(evidence, "Liens", profile.Liens.Count);
            AddCount(evidence, "Demands", profile.Demands.Count);
            AddCount(evidence, "Settlements", profile.Settlements.Count);
            AddCount(evidence, "Witnesses", profile.Witnesses.Count);
        }

        return new MatterContextSnapshot(
            matter.DecisionMatterId,
            tenantId,
            originalQuestion ?? string.Empty,
            matter.DomainPackCode,
            matter.PracticeAreaCode,
            decision,
            legalScope,
            piProfile,
            facts,
            evidence);
    }

    private static MatterContextField Supplied(string label, string? value)
        => new(label, string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
            string.IsNullOrWhiteSpace(value) ? MatterFieldProvenance.Unknown : MatterFieldProvenance.Supplied);

    private static void AddCount(List<MatterContextField> target, string label, int count)
    {
        if (count > 0)
            target.Add(new MatterContextField(label, count.ToString(), MatterFieldProvenance.Supplied));
    }
}
