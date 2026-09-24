using System;
using System.Linq;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ──────────────────────────────────────────────────────────────────────────────────────────────
// R2 regression tests — the selected Matter is a first-class, structured proposal input.
//
// These prove the immutable Matter Context Snapshot:
//   • preserves the original free-text question SEPARATELY (never merged/suppressed),
//   • keeps GoverningLaw, Jurisdiction, State, IncidentState, and forum fields DISTINCT end-to-end,
//   • never derives Jurisdiction from GoverningLaw or IncidentState,
//   • marks supplied allegations (Liability/Injury/Damages) as SUPPLIED, never VERIFIED.
// ──────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MatterContextSnapshotBuilderTests
{
    private static DecisionMatterDto BuildMatter() => new(
        DecisionMatterId: Guid.NewGuid(),
        Title: "Thompson v. Acme Premises Liability",
        MatterTypeCode: "PREMISES_LIABILITY",
        Jurisdiction: "Orange County Superior Court",
        Posture: "Pre-litigation",
        Description: "Slip and fall at a retail store.",
        StatusCode: "ACTIVE",
        CreatedDateUtc: DateTime.UtcNow,
        ModifiedDateUtc: null)
    {
        PracticeAreaCode = "PERSONAL_INJURY",
        DomainPackCode = "PERSONAL_INJURY",
        GoverningLaw = "Delaware",
        State = "California",
        CourtSystem = "State",
        CourtLevel = "Superior",
        County = "Orange",
        SubjectMatterJurisdiction = "General civil",
        PersonalTerritorialJurisdiction = "Resident defendant",
        ProceduralLaw = "California Code of Civil Procedure",
        AuthorityCutoffDate = new DateOnly(2024, 1, 1),
        RequestedDisposition = "Full-value settlement",
    };

    private static PersonalInjuryProfileDto BuildProfile(Guid matterId) => new(matterId)
    {
        IncidentTypeCode = "SLIP_AND_FALL",
        IncidentDate = new DateOnly(2023, 6, 15),
        IncidentState = "Nevada",
        LiabilitySummary = "Store failed to maintain safe premises.",
        InjurySummary = "Fractured wrist.",
        DamagesSummary = "Medical bills and lost wages.",
        CurrentStageCode = "PRE_LITIGATION",
    };

    [Fact]
    public void Build_PreservesOriginalQuestionSeparately_FromStructuredContext()
    {
        var matter = BuildMatter();
        const string question = "Can we establish liability for failure to maintain safe premises?";

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), question, matter, BuildProfile(matter.DecisionMatterId));

        // The original question is preserved verbatim and separately from the matter title/description.
        Assert.Equal(question, snapshot.OriginalQuestion);
        var questionField = snapshot.Decision.Single(f => f.Label == "Original Question");
        Assert.Equal(question, questionField.Value);
        Assert.NotEqual(matter.Title, questionField.Value);
    }

    [Fact]
    public void Build_KeepsGoverningLaw_And_Jurisdiction_And_State_Distinct()
    {
        var matter = BuildMatter();

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), "q", matter, null);

        var governingLaw = snapshot.LegalScope.Single(f => f.Label == "Governing Law");
        var jurisdiction = snapshot.LegalScope.Single(f => f.Label.StartsWith("Jurisdiction", StringComparison.Ordinal));
        var state = snapshot.LegalScope.Single(f => f.Label == "State");

        Assert.Equal("Delaware", governingLaw.Value);
        Assert.Equal("Orange County Superior Court", jurisdiction.Value);
        Assert.Equal("California", state.Value);

        // Never derive Jurisdiction from GoverningLaw or State.
        Assert.NotEqual(governingLaw.Value, jurisdiction.Value);
        Assert.NotEqual(state.Value, jurisdiction.Value);
    }

    [Fact]
    public void Build_KeepsIncidentState_Distinct_From_Forum_And_GoverningLaw()
    {
        var matter = BuildMatter();

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), "q", matter, BuildProfile(matter.DecisionMatterId));

        var incidentState = snapshot.PersonalInjuryProfile.Single(f => f.Label.StartsWith("Incident State", StringComparison.Ordinal));
        var governingLaw = snapshot.LegalScope.Single(f => f.Label == "Governing Law");
        var jurisdiction = snapshot.LegalScope.Single(f => f.Label.StartsWith("Jurisdiction", StringComparison.Ordinal));

        Assert.Equal("Nevada", incidentState.Value);
        // IncidentState must never be conflated with GoverningLaw (Delaware) or the saved forum.
        Assert.NotEqual(governingLaw.Value, incidentState.Value);
        Assert.NotEqual(jurisdiction.Value, incidentState.Value);
    }

    [Fact]
    public void Build_MarksAllegationsAsSupplied_NeverVerified()
    {
        var matter = BuildMatter();

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), "q", matter, BuildProfile(matter.DecisionMatterId));

        var liability = snapshot.Facts.Single(f => f.Label.StartsWith("Liability Summary", StringComparison.Ordinal));
        Assert.Equal(MatterFieldProvenance.Supplied, liability.Provenance);
        Assert.NotEqual(MatterFieldProvenance.Verified, liability.Provenance);
        Assert.DoesNotContain(snapshot.Facts, f => f.Provenance == MatterFieldProvenance.Verified);
    }

    [Fact]
    public void ToPromptBlock_RendersDistinctLabels_WithProvenanceTags()
    {
        var matter = BuildMatter();

        var block = MatterContextSnapshotBuilder
            .Build(Guid.NewGuid(), "q", matter, BuildProfile(matter.DecisionMatterId))
            .ToPromptBlock();

        Assert.Contains("Governing Law: Delaware", block);
        Assert.Contains("California", block);
        Assert.Contains("Nevada", block);
        Assert.Contains("[SUPPLIED]", block);
        // GoverningLaw value must not be the value rendered for the saved Jurisdiction reference.
        Assert.Contains("Jurisdiction (saved reference): Orange County Superior Court", block);
    }

    [Fact]
    public void Build_WithoutProfile_StillProducesLegalScopeContext()
    {
        var matter = BuildMatter();

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), "q", matter, null);

        Assert.True(snapshot.HasAnyContext);
        Assert.Empty(snapshot.PersonalInjuryProfile.Where(f => f.HasValue));
        Assert.Contains(snapshot.LegalScope, f => f.Label == "Governing Law" && f.HasValue);
    }

    // #5 — the Domain Pack is a first-class routing input and must be rendered into the prompt block.
    [Fact]
    public void ToPromptBlock_RendersDomainPack_ForDiscoveryAndGraphPrompts()
    {
        var matter = BuildMatter();

        var block = MatterContextSnapshotBuilder
            .Build(Guid.NewGuid(), "q", matter, BuildProfile(matter.DecisionMatterId))
            .ToPromptBlock();

        Assert.Contains("MATTER DOMAIN", block);
        Assert.Contains("Domain Pack: PERSONAL_INJURY", block);
        Assert.Contains("Practice Area: PERSONAL_INJURY", block);
    }

    // #7 — a short/title-only question must still carry the full saved Matter context.
    [Fact]
    public void Build_WithTitleOnlyQuestion_StillProjectsFullMatterContext()
    {
        var matter = BuildMatter();
        // The UI seeds only the readable matter title as the default question.
        var titleOnlyQuestion = matter.Title;

        var snapshot = MatterContextSnapshotBuilder.Build(Guid.NewGuid(), titleOnlyQuestion, matter, BuildProfile(matter.DecisionMatterId));
        var block = snapshot.ToPromptBlock();

        Assert.Equal(matter.Title, snapshot.OriginalQuestion);
        Assert.True(snapshot.FieldCount > 5);
        // Incident, jurisdiction, injuries, and demand-facing context must all reach the prompt even
        // though the user typed only the title — this is the exact "not supplied" regression.
        Assert.Contains("Governing Law: Delaware", block);
        Assert.Contains("Incident Date: 2023-06-15", block);
        Assert.Contains("Injury Summary (alleged): Fractured wrist.", block);
        Assert.Contains("Incident State (location, not forum): Nevada", block);
    }

    // #8 — GoverningLaw, Jurisdiction, State, and IncidentState must never be substituted for one another.
    [Fact]
    public void ToPromptBlock_NeverSubstitutesDistinctLegalFields()
    {
        var matter = BuildMatter();

        var block = MatterContextSnapshotBuilder
            .Build(Guid.NewGuid(), "q", matter, BuildProfile(matter.DecisionMatterId))
            .ToPromptBlock();

        // Each distinct legal field renders its own saved value under its own label.
        Assert.Contains("Governing Law: Delaware", block);
        Assert.Contains("Jurisdiction (saved reference): Orange County Superior Court", block);
        Assert.Contains("State: California", block);
        Assert.Contains("Incident State (location, not forum): Nevada", block);
        // Governing Law must not be silently rendered as the Jurisdiction/State/IncidentState value.
        Assert.DoesNotContain("Governing Law: California", block);
        Assert.DoesNotContain("Governing Law: Nevada", block);
        Assert.DoesNotContain("Governing Law: Orange County Superior Court", block);
    }
}
