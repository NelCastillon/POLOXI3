using System.Reflection;
using Ams.Application;
using Ams.Application.Features.Intelligence;
using Xunit;

namespace Ams.Application.Tests;

public sealed class WideDeliverableSynthesisTests
{
    [Fact]
    public void ShouldSynthesizeDeliverable_IncludesResolutionLikeAmbiguityGroups()
    {
        var method=PrivateMethod("ShouldSynthesizeDeliverable");
        var configuration=Configuration("configured outcome");
        var contract=new WideQueryContract(null,null,null,null,[],[],["configured outcome"]){AnswerKind="ENTITY_RANKING",CandidateKind="NAMED_ENTITY"};
        var groups=new[]
        {
            Group("PRIMARY_MEANING","Configured Outcome Format","Determine the configured outcome format."),
            Group("SECONDARY_MEANING","Supporting Basis","Determine the supporting basis.")
        };

        var result=(bool)method.Invoke(null,[configuration,contract,groups,Array.Empty<WideCandidateDto>()])!;

        Assert.True(result);
    }

    [Fact]
    public void BuildResolutionDeliverable_ReportsPartialWhenResolutionMeaningsRemainAmbiguous()
    {
        var method=PrivateMethod("BuildResolutionDeliverable");
        var configuration=Configuration("configured outcome","supporting basis");
        var contract=new WideQueryContract(null,null,null,null,[],["configured outcome","supporting basis"],["configured outcome","supporting basis"])
        {
            AnswerKind="ENTITY_RANKING",
            CandidateKind="NAMED_ENTITY",
            TargetObject="the configured determination"
        };
        var groups=new[]
        {
            Group("PRIMARY_MEANING","Configured Outcome Format","Determine the configured outcome format."),
            Group("SECONDARY_MEANING","Supporting Basis","Determine the supporting basis.")
        };
        var evidence=new[]
        {
            new PoloxiEvidenceDto(Guid.NewGuid(),Guid.NewGuid(),"Record",Guid.NewGuid(),"ConfiguredModule","Configured evidence packet","Configured evidence supports part of the determination, but required inputs remain incomplete.","/configured/test",0.92m,1,["PRIMARY_MEANING"])
        };
        var request=new WideSearchRequest(Guid.NewGuid(),Guid.NewGuid(),"determine the configured outcome and supporting basis");
        var entropy=new WideEntropyResult(0.8m,1m,0.8m,2);

        var deliverable=(WideResolutionDeliverableDto)method.Invoke(null,[request,configuration,contract,groups,evidence,Array.Empty<WideExternalKnowledgeSnippet>(),0.45m,0.6m,entropy])!;

        Assert.Equal("PARTIAL",deliverable.DeterminacyCode);
        Assert.Contains("multiple deliverable meanings remain possible",deliverable.Headline);
        Assert.Contains(deliverable.BlockingInputs,item=>item.Contains("Configured Outcome Format",StringComparison.OrdinalIgnoreCase)&&item.Contains("Supporting Basis",StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(deliverable.Citations);
    }

    [Fact]
    public void BuildIntentUserPrompt_ClampsUnboundedQueryAndContractToSafetyBudget()
    {
        var method=PrivateMethod("BuildIntentUserPrompt");

        var prompt=(string)method.Invoke(null,[new string('q',30000),new string('c',10000),new string('a',5000),12])!;

        Assert.True(prompt.Length<=12000);
        Assert.StartsWith("Ambiguous question: ",prompt);
        Assert.Contains("Maximum branches: 12",prompt);
        Assert.Contains("Approved capability catalog",prompt);
    }

    [Fact]
    public void BuildHierarchyUserPrompt_ClampsAllVariableSectionsToSafetyBudget()
    {
        var method=PrivateMethod("BuildHierarchyUserPrompt");

        var prompt=(string)method.Invoke(null,[new string('q',30000),new string('c',10000),new string('p',10000),new string('a',5000),2,12])!;

        Assert.True(prompt.Length<=12000);
        Assert.StartsWith("Original question: ",prompt);
        Assert.Contains("Level to propose: 2",prompt);
        Assert.Contains("Maximum branches per parent: 12",prompt);
    }

    [Theory]
    [InlineData("context","CONTEXT")]
    [InlineData("GUARDRAIL","GUARDRAIL")]
    [InlineData("hard_constraint","HARD_CONSTRAINT")]
    [InlineData(null,"PREFERENCE")]
    [InlineData("unknown","PREFERENCE")]
    public void NormalizeBranchRole_UsesSupportedRolesAndSafeFallback(string? value,string expected)
    {
        var method=PrivateMethod("NormalizeBranchRole");

        var result=(string)method.Invoke(null,[value])!;

        Assert.Equal(expected,result);
    }

    [Theory]
    [InlineData("CONTEXT","NonScoring")]
    [InlineData("HARD_CONSTRAINT","HardConstraint")]
    [InlineData("PREFERENCE","ScoreableCriterion")]
    [InlineData("GUARDRAIL","ScoreableCriterion")]
    public void ClassifyCompetitionRole_HonorsExplicitRole(string role,string expected)
    {
        var method=PrivateMethod("ClassifyCompetitionRole");
        var branch=Branch("Criterion","Ordinary evaluation criterion") with{BranchRoleCode=role};

        var result=method.Invoke(null,[branch,null]);

        Assert.Equal(expected,result!.ToString());
    }

    [Fact]
    public void MergeMissingBranchScores_FillsOnlyMissingCells()
    {
        var method=PrivateMethod("MergeMissingBranchScores");
        var quality=Branch("Quality","Quality criterion");
        var affordability=Branch("Affordability","Affordability criterion");
        var existing=new[]{new WideCandidateBranchEvidence("Quality",.8m)};
        var incoming=new[]{new WideCandidateBranchEvidence("Quality",.2m),new WideCandidateBranchEvidence("Affordability",.7m)};

        var merged=(IReadOnlyCollection<WideCandidateBranchEvidence>)method.Invoke(null,[existing,incoming,new[]{quality,affordability}])!;

        Assert.Equal(2,merged.Count);
        Assert.Equal(.8m,merged.Single(score=>score.BranchDisplayName=="Quality").EvidenceScore);
        Assert.Equal(.7m,merged.Single(score=>score.BranchDisplayName=="Affordability").EvidenceScore);
    }

    private static WideAmbiguityGroupDto Group(string code,string displayName,string interpretation)=>new(Guid.NewGuid(),code,displayName,interpretation,0.75m,null,"ENTITY_RANKING","NAMED_ENTITY")
    {
        Summary=interpretation
    };

    private static WideConfiguration Configuration(params string[] indicators)=>new(.72m,.30m,6,2,12)
    {
        DeliverableSynthesisIndicators=indicators
    };

    private static WideBranchRecord Branch(string displayName,string interpretation)=>new(Guid.NewGuid(),Guid.NewGuid(),null,Guid.NewGuid(),1,displayName.ToUpperInvariant(),displayName,interpretation,null,null,"PENDING",0,.8m,false,null,false,null,1);

    private static MethodInfo PrivateMethod(string name)=>typeof(IntelligenceWideService).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)??throw new MissingMethodException(nameof(IntelligenceWideService),name);
}
