using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionDomainGuardrailGovernanceTests
{
    [Fact]
    public void BranchPersistence_ProvidesParameterlessConstructorForDapperMaterialization()
    {
        var branch = new DecisionBranchPersistence
        {
            GenerationOriginCode = DecisionBranchGenerationOrigins.DynamicLlmEnriched,
            DecisionDomainConceptId = Guid.NewGuid(),
            DomainConceptCode = "COMPARATIVE_FAULT",
            GuardrailMatchScore = 0.95m,
            GuardrailActionCode = DecisionGuardrailActions.ConceptMatched,
            GuardrailVersion = 1,
        };

        Assert.Equal(DecisionBranchGenerationOrigins.DynamicLlmEnriched, branch.GenerationOriginCode);
        Assert.Equal("COMPARATIVE_FAULT", branch.DomainConceptCode);
        Assert.True(branch.IsExecutable);
    }

    [Fact]
    public void MatchingConcept_EnrichesProvenanceWithoutChangingDynamicScoresOrFrontier()
    {
        var branch = Branch("C1.B1", "Allocation at comparative-fault threshold", "Compare plaintiff fault to the governing threshold.");
        var concept = Concept("COMPARATIVE_FAULT_THRESHOLD", "Comparative-fault threshold",
            "The governing jurisdiction threshold and legal consequence for attributed fault.", required: false);

        var result = LegalDecisionService.ApplyDomainGuardrails(
            "Does comparative fault reduce recovery?", [Candidate()], [branch], [concept], []);

        var governed = Assert.Single(result.Branches);
        Assert.Equal(DecisionBranchGenerationOrigins.DynamicLlmEnriched, governed.GenerationOriginCode);
        Assert.Equal(concept.DecisionDomainConceptId, governed.DecisionDomainConceptId);
        Assert.Equal(concept.ConceptCode, governed.DomainConceptCode);
        Assert.Equal(DecisionGuardrailActions.ConceptMatched, governed.GuardrailActionCode);
        Assert.Equal(branch.InformationValue, governed.InformationValue);
        Assert.Equal(branch.DecisionRelevance, governed.DecisionRelevance);
        Assert.Equal(branch.FlipPotential, governed.FlipPotential);
        Assert.Equal(branch.IsOnFrontier, governed.IsOnFrontier);
        Assert.Equal(branch.BranchStateCode, governed.BranchStateCode);
    }

    [Fact]
    public void UnmatchedDynamicBranch_RemainsValidNovelBranch()
    {
        var branch = Branch("C1.B1", "Unusual decision-specific distinction", "Unique to this matter.");
        var concept = Concept("MEDICAL_CAUSATION", "Medical causation", "Incident to injury relationship.", required: false);

        var result = LegalDecisionService.ApplyDomainGuardrails(
            "A novel procedural issue", [Candidate()], [branch], [concept], []);

        var governed = Assert.Single(result.Branches);
        Assert.Equal(DecisionBranchGenerationOrigins.DynamicLlm, governed.GenerationOriginCode);
        Assert.Equal(DecisionGuardrailActions.NovelAccepted, governed.GuardrailActionCode);
        Assert.Null(governed.DecisionDomainConceptId);
        Assert.Equal(1, result.NovelBranchCount);
        Assert.Equal(0, result.FallbackBranchCount);
    }

    [Fact]
    public void MissingRequiredRelevantConcept_AddsDormantNonCompetingFallback()
    {
        var branch = Branch("C1.B1", "Settlement approval", "Whether settlement requires approval.");
        var required = Concept("COMPARATIVE_FAULT", "Comparative fault",
            "Allocation of legally attributable causal fault among the plaintiff and defendant.", required: true);

        var result = LegalDecisionService.ApplyDomainGuardrails(
            "How does comparative fault allocation affect plaintiff recovery?", [Candidate()], [branch], [required], []);

        var fallback = Assert.Single(result.Branches.Where(item =>
            item.GenerationOriginCode == DecisionBranchGenerationOrigins.DomainFallback));
        Assert.Equal(DecisionBranchStates.Dormant, fallback.BranchStateCode);
        Assert.False(fallback.IsOnFrontier);
        Assert.Equal(0m, fallback.InformationValue);
        Assert.Equal(0m, fallback.AdvScore);
        Assert.Equal("DOMAIN_FALLBACK_NOT_ACTIVATED", fallback.StopReason);
        Assert.Equal(DecisionGuardrailActions.FallbackAdded, fallback.GuardrailActionCode);
        Assert.Equal(required.ConceptCode, fallback.DomainConceptCode);
    }

    [Fact]
    public void MissingRequiredIrrelevantConcept_DoesNotForceTaxonomyIntoHierarchy()
    {
        var branch = Branch("C1.B1", "Settlement approval", "Whether settlement requires approval.");
        var required = Concept("MEDICAL_CAUSATION", "Medical causation",
            "Relationship between incident biomechanics and diagnosed injury.", required: true);

        var result = LegalDecisionService.ApplyDomainGuardrails(
            "Is the written settlement enforceable?", [Candidate()], [branch], [required], []);

        Assert.DoesNotContain(result.Branches, item =>
            item.GenerationOriginCode == DecisionBranchGenerationOrigins.DomainFallback);
        Assert.Equal(0, result.FallbackBranchCount);
    }

    [Fact]
    public void HardConstraint_MarksMatchedBranchAsConstraintEnriched()
    {
        var branch = Branch("C1.B1", "Comparative fault", "Allocate causal fault.");
        var concept = Concept("COMPARATIVE_FAULT", "Comparative fault", "Allocate causal fault among actors.", required: true);
        var relation = new DecisionDomainConceptRelationDto(
            Guid.NewGuid(), concept.ConceptCode, "FAULT_EVIDENCE", "REQUIRES", "MATTER_EVIDENCE_REQUIRED",
            "Requires matter evidence.", null, null, IsHardConstraint: true, SortOrder: 1);

        var result = LegalDecisionService.ApplyDomainGuardrails(
            "Determine comparative fault", [Candidate()], [branch], [concept], [relation]);

        Assert.Equal(DecisionGuardrailActions.ConstraintEnriched, Assert.Single(result.Branches).GuardrailActionCode);
    }

    [Fact]
    public void ExecutableBranchFilter_ExcludesDormantDomainFallbackFromDownstreamWork()
    {
        var dynamicBranch = Branch("C1.B1", "Dynamic", "Dynamic branch.");
        var fallback = Branch("DOMAIN.FALLBACK", "Fallback", "Audit-only fallback.") with
        {
            BranchStateCode = DecisionBranchStates.Dormant,
            GenerationOriginCode = DecisionBranchGenerationOrigins.DomainFallback,
            IsOnFrontier = false,
        };

        Assert.True(dynamicBranch.IsExecutable);
        Assert.False(fallback.IsExecutable);
    }

    private static DecisionCandidatePersistence Candidate() => new(
        Guid.NewGuid(), "C1", "Candidate", "Outcome", 0.5m, 0.5m, 0.5m, 0.5m, 0.5m,
        0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 1, true, false);

    private static DecisionBranchPersistence Branch(string code, string name, string interpretation) => new(
        Guid.NewGuid(), null, 1, code, name, interpretation, DecisionBranchStates.Active,
        0.8m, 0.95m, 1m, 0.5m, 0.76m, 1m, true, null, 0);

    private static DecisionDomainConceptDto Concept(string code, string name, string description, bool required) => new(
        Guid.NewGuid(), code, "DEFENSES", name, description, "MIXED", "LEGAL_AUTHORITY", null,
        null, null, required, true, 1, 1);
}
