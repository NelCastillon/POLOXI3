using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Essential-evidence readiness gate (migration 0451, table
// Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement). Proves that the service separates
// SCORED coverage from VERIFIED evidence coverage: a candidate whose outcome declares gating evidence
// requirements is only decision-ready when that evidence is actually present in the matter. Presence is
// resolved either by a direct document classification (_matterDocumentEvidenceTypeCodes) or by the
// DB-backed evidence-type name/description matching the matter presence corpus. The gate is advisory and
// never alters the composite score — it only discloses unmet gating requirements.
//
// Mirrors LegalDecisionCanonicalOutcomeCandidateTests: the heavy 10-dependency constructor is bypassed
// with GetUninitializedObject because ResolveEssentialEvidenceReadiness is pure deterministic logic that
// reads only the injected pack fields.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionEssentialEvidenceReadinessTests
{
    private static DecisionDomainPackOutcomeCandidateDto Outcome(string code, string name)
        => new(code, name, null, "PATHWAY", false, "PERSONAL_INJURY", 10, false, null, null, "MATTER_RESOLUTION", "MATTER_RESOLUTION");

    private static DecisionDomainPackEvidenceTypeDto Evidence(string code, string name, string? description = null)
        => new(code, name, "DAMAGES", description);

    private static DecisionDomainPackOutcomeCandidateEvidenceRequirementDto Requirement(string outcomeCode, string evidenceTypeCode, bool essential)
        => new(outcomeCode, evidenceTypeCode, essential, null, null, 0);

    private static DecisionDomainPackOutcomeCandidateEvidenceRequirementDto VerifiedRequirement(string outcomeCode, string evidenceTypeCode, string profileCode)
        => new(outcomeCode, evidenceTypeCode, true, profileCode, null, 0);

    private static IntelligenceWide2Service NewService(
        IReadOnlyList<DecisionDomainPackOutcomeCandidateDto> candidates,
        IReadOnlyList<DecisionDomainPackOutcomeCandidateEvidenceRequirementDto> requirements,
        IReadOnlyList<DecisionDomainPackEvidenceTypeDto> evidenceTypes,
        ISet<string> directEvidenceTypeCodes,
        ISet<string> presenceTokens,
        IDictionary<string, int>? verifiedAssertionCounts = null)
    {
        var service = (IntelligenceWide2Service)RuntimeHelpers.GetUninitializedObject(typeof(IntelligenceWide2Service));
        Set(service, "_canonicalOutcomeCandidates", candidates);
        Set(service, "_outcomeCandidateEvidenceRequirements", requirements);
        Set(service, "_packEvidenceTypes", evidenceTypes);
        Set(service, "_matterDocumentEvidenceTypeCodes", directEvidenceTypeCodes);
        Set(service, "_matterPresenceTokens", presenceTokens);
        Set(service, "_verifiedAssertionCountsByEvidenceType", verifiedAssertionCounts ?? new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase));
        return service;
    }

    private static void Set(object target, string field, object value)
        => typeof(IntelligenceWide2Service).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(target, value);

    private static (int Required, int Satisfied, IReadOnlyList<string> Missing) Invoke(IntelligenceWide2Service service, string name)
    {
        var result = typeof(IntelligenceWide2Service)
            .GetMethod("ResolveEssentialEvidenceReadiness", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [name])!;
        var type = result.GetType();
        var required = (int)type.GetField("Item1")!.GetValue(result)!;
        var satisfied = (int)type.GetField("Item2")!.GetValue(result)!;
        var missing = (IReadOnlyList<string>)type.GetField("Item3")!.GetValue(result)!;
        return (required, satisfied, missing);
    }

    [Fact]
    public void Candidate_With_No_Configured_Requirements_Is_Always_Ready()
    {
        var service = NewService(
            [Outcome("M1", "Negotiated settlement")],
            requirements: [],
            evidenceTypes: [],
            directEvidenceTypeCodes: new HashSet<string>(),
            presenceTokens: new HashSet<string>());

        var (required, satisfied, missing) = Invoke(service, "Negotiated settlement");

        Assert.Equal(0, required);
        Assert.Equal(0, satisfied);
        Assert.Empty(missing);
    }

    [Fact]
    public void Missing_Essential_Evidence_Is_Reported_With_Human_Label()
    {
        var service = NewService(
            [Outcome("M3", "Plaintiff judgment")],
            requirements: [Requirement("M3", "MEDICAL_RECORDS", essential: true)],
            evidenceTypes: [Evidence("MEDICAL_RECORDS", "Medical Records")],
            directEvidenceTypeCodes: new HashSet<string>(),
            presenceTokens: new HashSet<string>());

        var (required, satisfied, missing) = Invoke(service, "Plaintiff judgment");

        Assert.Equal(1, required);
        Assert.Equal(0, satisfied);
        Assert.Equal("Medical Records", Assert.Single(missing));
    }

    [Fact]
    public void Direct_Document_Classification_Satisfies_Essential_Requirement()
    {
        var service = NewService(
            [Outcome("M3", "Plaintiff judgment")],
            requirements: [Requirement("M3", "MEDICAL_RECORDS", essential: true)],
            evidenceTypes: [Evidence("MEDICAL_RECORDS", "Medical Records")],
            directEvidenceTypeCodes: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "MEDICAL_RECORDS" },
            presenceTokens: new HashSet<string>());

        var (required, satisfied, missing) = Invoke(service, "Plaintiff judgment");

        Assert.Equal(1, required);
        Assert.Equal(1, satisfied);
        Assert.Empty(missing);
    }

    [Fact]
    public void Corpus_Name_Match_Satisfies_Essential_Requirement()
    {
        // "surveillance" is a specific (non-generic) name term; its presence in the matter corpus satisfies
        // the requirement via DomainPackEvidencePresenceMatcher without a direct document classification.
        var tokens = new HashSet<string>(DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(
            "A surveillance video of the claimant was produced during discovery."));

        var service = NewService(
            [Outcome("M4", "Defense judgment / dismissal")],
            requirements: [Requirement("M4", "SURVEILLANCE_VIDEO", essential: true)],
            evidenceTypes: [Evidence("SURVEILLANCE_VIDEO", "Surveillance Video")],
            directEvidenceTypeCodes: new HashSet<string>(),
            presenceTokens: tokens);

        var (required, satisfied, missing) = Invoke(service, "Defense judgment / dismissal");

        Assert.Equal(1, required);
        Assert.Equal(1, satisfied);
        Assert.Empty(missing);
    }

    [Fact]
    public void Non_Essential_Requirements_Do_Not_Gate_Readiness()
    {
        var service = NewService(
            [Outcome("M1", "Negotiated settlement")],
            requirements: [Requirement("M1", "DEMAND_LETTER", essential: false)],
            evidenceTypes: [Evidence("DEMAND_LETTER", "Demand Letter")],
            directEvidenceTypeCodes: new HashSet<string>(),
            presenceTokens: new HashSet<string>());

        var (required, satisfied, missing) = Invoke(service, "Negotiated settlement");

        Assert.Equal(0, required);
        Assert.Equal(0, satisfied);
        Assert.Empty(missing);
    }

    [Fact]
    public void Partial_Satisfaction_Reports_Only_The_Unmet_Requirement()
    {
        var service = NewService(
            [Outcome("M3", "Plaintiff judgment")],
            requirements:
            [
                Requirement("M3", "MEDICAL_RECORDS", essential: true),
                Requirement("M3", "LIFE_CARE_PLAN", essential: true),
            ],
            evidenceTypes:
            [
                Evidence("MEDICAL_RECORDS", "Medical Records"),
                Evidence("LIFE_CARE_PLAN", "Life Care Plan"),
            ],
            directEvidenceTypeCodes: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "MEDICAL_RECORDS" },
            presenceTokens: new HashSet<string>());

        var (required, satisfied, missing) = Invoke(service, "Plaintiff judgment");

        Assert.Equal(2, required);
        Assert.Equal(1, satisfied);
        Assert.Equal("Life Care Plan", Assert.Single(missing));
    }

    [Fact]
    public void Present_But_Unverified_Fails_A_Verification_Profile_Requirement()
    {
        // The document is classified (present) but has no VERIFIED source assertion. A requirement that names
        // a MinVerificationProfileCode must NOT count presence alone as decision-ready; the gap is disclosed
        // with an "(unverified)" qualifier so the user knows to verify rather than re-upload.
        var service = NewService(
            [Outcome("P1", "Granted in full")],
            requirements: [VerifiedRequirement("P1", "ACCIDENT_REPORT", "LIABILITY_FACTS")],
            evidenceTypes: [Evidence("ACCIDENT_REPORT", "Accident Report")],
            directEvidenceTypeCodes: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ACCIDENT_REPORT" },
            presenceTokens: new HashSet<string>(),
            verifiedAssertionCounts: new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase));

        var (required, satisfied, missing) = Invoke(service, "Granted in full");

        Assert.Equal(1, required);
        Assert.Equal(0, satisfied);
        Assert.Equal("Accident Report (unverified)", Assert.Single(missing));
    }

    [Fact]
    public void Present_And_Verified_Satisfies_A_Verification_Profile_Requirement()
    {
        // Same requirement, but now the evidence type carries a VERIFIED source assertion, so the stronger
        // verification gate is satisfied and the candidate is decision-ready.
        var service = NewService(
            [Outcome("P1", "Granted in full")],
            requirements: [VerifiedRequirement("P1", "ACCIDENT_REPORT", "LIABILITY_FACTS")],
            evidenceTypes: [Evidence("ACCIDENT_REPORT", "Accident Report")],
            directEvidenceTypeCodes: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ACCIDENT_REPORT" },
            presenceTokens: new HashSet<string>(),
            verifiedAssertionCounts: new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase) { ["ACCIDENT_REPORT"] = 2 });

        var (required, satisfied, missing) = Invoke(service, "Granted in full");

        Assert.Equal(1, required);
        Assert.Equal(1, satisfied);
        Assert.Empty(missing);
    }
}
