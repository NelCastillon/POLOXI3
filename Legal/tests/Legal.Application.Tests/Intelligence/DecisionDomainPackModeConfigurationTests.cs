using System;
using System.Collections.Generic;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Covers the DomainPack (checked) discovery-mode ONLY payload builder. These tests assert that the
// COMPLETE active Domain Pack configuration reaches the LLM and that the advisory/authority boundaries
// required by the review matrix are present. They do not touch the original (unchecked) discovery path.
public sealed class DecisionDomainPackModeConfigurationTests
{
    [Fact]
    public void Serializes_All_Active_Pack_Collections_Into_SelectedDomainPack_Block()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: "PI_AUTO");

        Assert.Contains("SELECTED_DOMAIN_PACK", result);
        // Dimensions
        Assert.Contains("LIABILITY", result);
        // EvidenceTypes
        Assert.Contains("POLICE_REPORT", result);
        // VerificationProfiles
        Assert.Contains("CARRIER_API_PROFILE", result);
        // MatterTypes
        Assert.Contains("PI_AUTO", result);
        // OutcomeCandidates
        Assert.Contains("C1", result);
        // Concepts
        Assert.Contains("COMPARATIVE_FAULT", result);
        // Relations
        Assert.Contains("FAULT_EVIDENCE", result);
    }

    [Fact]
    public void Declares_Numeric_Scores_As_Advisory_And_Core_Owned()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("advisory model proposals", result);
        Assert.Contains("POLOXI", result);
    }

    [Fact]
    public void Declares_HardConstraint_As_Configuration_Not_Exclusion()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("IsHardConstraint", result);
        Assert.Contains("NOT a conclusion that a candidate is excluded", result);
    }

    [Fact]
    public void Describes_Configuration_As_Not_Evidence()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("NOT evidence", result);
    }

    [Fact]
    public void Preserves_OutcomeCandidate_Identity_And_Role()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("PATHWAY", result);
        Assert.Contains("ASSERTED_HISTORICAL", result);
    }

    [Fact]
    public void Expresses_Recursive_Hierarchy_Language()
    {
        var pack = Pack();

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("L1", result);
        Assert.Contains("Ln", result);
    }

    [Fact]
    public void Empty_Collections_Do_Not_Throw()
    {
        var pack = new DecisionDomainPackDto(
            Guid.NewGuid(), "EMPTY_PACK", "PI", "Empty Pack", null,
            Dimensions: [], EvidenceTypes: [], VerificationProfiles: [], MatterTypes: []);

        var result = LegalDecisionService.BuildDomainPackModeConfiguration(
            pack, pack.Concepts, pack.ConceptRelations, matterTypeCode: null);

        Assert.Contains("SELECTED_DOMAIN_PACK", result);
        Assert.Contains("EMPTY_PACK", result);
    }

    private static DecisionDomainPackDto Pack() => new(
        Guid.NewGuid(),
        "PI_PACK",
        "PI",
        "Personal Injury Pack",
        "Advisory configuration.",
        Dimensions:
        [
            new DecisionDomainPackDimensionDto("LIABILITY", "Liability", "Fault allocation."),
            new DecisionDomainPackDimensionDto("DAMAGES", "Damages", "Economic and non-economic harm."),
        ],
        EvidenceTypes:
        [
            new DecisionDomainPackEvidenceTypeDto("POLICE_REPORT", "Police Report", "LIABILITY", "Official crash report."),
        ],
        VerificationProfiles:
        [
            new DecisionDomainPackVerificationProfileDto("CARRIER_API_PROFILE", "Carrier API", "POLICE_REPORT", "Carrier API check."),
        ],
        MatterTypes:
        [
            new DecisionDomainPackMatterTypeDto("PI_AUTO", "Auto Injury", "Motor vehicle injury."),
        ])
    {
        Concepts =
        [
            new DecisionDomainConceptDto(
                Guid.NewGuid(), "COMPARATIVE_FAULT", "LIABILITY", "Comparative fault",
                "Allocation of attributable fault.", "FACTOR", "MATTER", null, null, null,
                IsRequiredCoverage: true, IsFallbackEligible: true, SortOrder: 1, VersionNumber: 1),
        ],
        ConceptRelations =
        [
            new DecisionDomainConceptRelationDto(
                Guid.NewGuid(), "COMPARATIVE_FAULT", "FAULT_EVIDENCE", "REQUIRES", "MATTER_EVIDENCE_REQUIRED",
                "Requires matter evidence.", null, null, IsHardConstraint: true, SortOrder: 1),
        ],
        OutcomeCandidates =
        [
            new DecisionDomainPackOutcomeCandidateDto(
                "C1", "Plaintiff Prevails", "Full recovery pathway.", "PATHWAY",
                RequiresVerification: false, "PI_AUTO", SortOrder: 1),
            new DecisionDomainPackOutcomeCandidateDto(
                "C5", "Already Settled", "Historical settlement to verify.", "ASSERTED_HISTORICAL",
                RequiresVerification: true, "PI_AUTO", SortOrder: 5),
        ],
    };
}
