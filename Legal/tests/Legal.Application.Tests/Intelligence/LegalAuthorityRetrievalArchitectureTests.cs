using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Infrastructure.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalAuthorityRetrievalArchitectureTests
{
    [Fact]
    public void Planner_ExactCitationIsFirst_AndOperationsAreBudgetBounded()
    {
        var need=Need("The governing rule is 42 U.S.C. § 1983.") with
        {
            SearchQuery="42 U.S.C. § 1983 civil remedy",
            SearchConceptsJson="[\"civil rights\",\"state action\"]",
            AuthorityKindsJson="[\"STATUTE\"]",
        };
        var plan=new LegalResearchPlanner().Plan(Request(need,3));

        Assert.Equal(3,plan.Operations.Count);
        Assert.Equal(LegalSearchOperationKind.ExactAuthority,plan.Operations[0].Kind);
        Assert.Contains(plan.Operations,operation=>operation.Kind==LegalSearchOperationKind.Lexical);
        Assert.Contains(plan.Operations,operation=>operation.Kind==LegalSearchOperationKind.Semantic);
    }

    [Fact]
    public void Planner_MixedCaseAndStatuteNeed_SearchesAllAuthorityProviders()
    {
        var need=Need("Delaware comparative negligence is governed by statute and controlling opinions.") with
        {
            AuthorityKindsJson="[\"STATUTE\",\"CASE_LAW\"]",
        };

        var plan=new LegalResearchPlanner().Plan(Request(need,3));

        Assert.NotEmpty(plan.Operations);
        Assert.All(plan.Operations,operation=>Assert.Equal(LegalAuthorityKind.Any,operation.AuthorityKind));
    }

    [Fact]
    public async Task Retrieval_DeduplicatesCanonicalUrlsAcrossOperations_AndRetainsLineage()
    {
        var operations=new[]
        {
            new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Lexical,"query one",LegalAuthorityKind.Statute,1),
            new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Semantic,"query two",LegalAuthorityKind.Statute,2),
        };
        var planner=new StubPlanner(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"proposition",null,DateOnly.MaxValue,operations));
        var source=new WideExternalKnowledgeSnippet("q","Authority","HTTPS://CODE.EXAMPLE.GOV/path/?tracking=1","Controlling passage",1m,DateTime.UtcNow)
        {AuthorityKind="STATUTE",SourceProvider="OFFICIAL",ProviderIdentityVerified=true};
        var retriever=new StubRetriever([source],new("OFFICIAL",true,"RESULTS_FOUND",1,1));
        var result=await new LegalAuthorityRetrievalService(planner,retriever).RetrieveAsync(Request(Need("proposition"),2),Configuration());

        var authority=Assert.Single(result.Authorities);
        Assert.Equal(2,authority.SearchOperationIds.Count);
        Assert.Equal(EvidenceSourceType.Statute,authority.ToRetrievedSource().SourceType);
        Assert.Equal(LegalRetrievalOutcome.ResultsFound,result.Outcome);
    }

    [Theory]
    [InlineData("ACCESS_DENIED",LegalRetrievalOutcome.AccessDenied)]
    [InlineData("PROVIDER_FAILURE",LegalRetrievalOutcome.ProviderFailure)]
    [InlineData("COVERAGE_GAP",LegalRetrievalOutcome.CoverageGap)]
    [InlineData("NO_RESULTS",LegalRetrievalOutcome.NoResults)]
    [InlineData("INVALID_QUERY",LegalRetrievalOutcome.InvalidQuery)]
    [InlineData("PARSE_REJECTED",LegalRetrievalOutcome.ParsingFailure)]
    [InlineData("FILTER_REJECTED",LegalRetrievalOutcome.FilteredOut)]
    public async Task Retrieval_PreservesOutcomeTaxonomy(string code,LegalRetrievalOutcome expected)
    {
        var operation=new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Lexical,"query",LegalAuthorityKind.Any,1);
        var planner=new StubPlanner(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"proposition",null,DateOnly.MaxValue,[operation]));
        var retriever=new StubRetriever([],new("PROVIDER",true,code,0,0));

        var result=await new LegalAuthorityRetrievalService(planner,retriever).RetrieveAsync(Request(Need("proposition"),1),Configuration());

        Assert.Equal(expected,result.Outcome);
    }

    [Fact]
    public async Task Retrieval_RecordsOperationQueryAndBoundedRecoveryAction()
    {
        var first=new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Lexical,"narrow query",LegalAuthorityKind.Case,1);
        var second=new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Semantic,"broader query",LegalAuthorityKind.Case,2);
        var planner=new StubPlanner(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"proposition",null,DateOnly.MaxValue,[first,second]));
        var retriever=new StubRetriever([],new("PROVIDER",true,"NO_RESULTS",0,0));

        var result=await new LegalAuthorityRetrievalService(planner,retriever).RetrieveAsync(Request(Need("proposition"),2),Configuration());

        Assert.Equal(2,result.ProviderAttempts.Count);
        var firstAttempt=result.ProviderAttempts[0];
        Assert.Equal(LegalSearchOperationKind.Lexical,firstAttempt.OperationKind);
        Assert.Equal("narrow query",firstAttempt.Query);
        Assert.Equal("BROADEN_WITH_NEXT_PLANNED_OPERATION",firstAttempt.RecoveryActionCode);
        Assert.Equal("BOUNDED_SEARCH_EXHAUSTED",result.ProviderAttempts[1].RecoveryActionCode);
    }

    [Fact]
    public async Task Retrieval_DoesNotTreatFetchDateAsAuthorityDate()
    {
        var operation=new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.ExactAuthority,"citation",LegalAuthorityKind.Statute,1);
        var planner=new StubPlanner(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"proposition",null,new DateOnly(2000,1,1),[operation]));
        var source=new WideExternalKnowledgeSnippet("q","Historical authority","https://code.example.gov/historical","Historical text",1m,DateTime.UtcNow)
        {AuthorityKind="STATUTE"};

        var result=await new LegalAuthorityRetrievalService(planner,new StubRetriever([source],new("OFFICIAL",true,"RESULTS_FOUND",1,1)))
            .RetrieveAsync(Request(Need("proposition"),1) with{AuthorityCutoffDate=new(2000,1,1)},Configuration());

        Assert.Single(result.Authorities);
        Assert.Null(result.Authorities[0].AuthorityDate);
    }

    [Fact]
    public async Task Retrieval_ReranksPassagesAgainstAtomicPropositionBeforeApplyingAuthorityBudget()
    {
        var need=Need("Delaware comparative negligence reduces recovery in proportion to claimant fault.");
        var operation=new LegalSearchOperation(Guid.NewGuid(),LegalSearchOperationKind.Lexical,"Delaware comparative negligence",LegalAuthorityKind.Case,1);
        var plan=new LegalSearchPlan(Guid.NewGuid(),need.DecisionResearchNeedId,need.DecisionSessionId,need.TenantId,
            need.PropositionToResolve!,"Delaware",DateOnly.MaxValue,[operation])
        {
            AtomicPropositionId=need.DecisionResearchNeedId,
        };
        var unrelated=new WideExternalKnowledgeSnippet("q","Unrelated filing order","https://court.example/unrelated",
            "The court extends the deadline for filing an appellate brief.",1m,DateTime.UtcNow)
        {AuthorityKind="CASE_LAW",SourceProvider="COURTLISTENER",ProviderIdentityVerified=true};
        var relevant=new WideExternalKnowledgeSnippet("q","Delaware comparative negligence","https://court.example/relevant",
            "Under Delaware comparative negligence, claimant fault reduces recovery in proportion to that fault.",0m,DateTime.UtcNow)
        {AuthorityKind="CASE_LAW",SourceProvider="COURTLISTENER",ProviderIdentityVerified=true};

        var result=await new LegalAuthorityRetrievalService(
                new StubPlanner(plan),
                new StubRetriever([unrelated,relevant],new("COURTLISTENER",true,"RESULTS_FOUND",2,2)))
            .RetrieveAsync(Request(need,1) with{MaximumAuthorities=1},Configuration());

        var selected=Assert.Single(result.Authorities);
        Assert.Equal("https://court.example/relevant",selected.SourceRef);
        Assert.Equal(1,selected.PropositionSelectionRank);
        Assert.True(selected.PropositionSelectionScore>0.5m);
        Assert.Equal(need.DecisionResearchNeedId,selected.AtomicPropositionId);
        Assert.Equal(plan.LegalSearchPlanId,selected.LegalSearchPlanId);
        Assert.False(string.IsNullOrWhiteSpace(selected.PassageIdentity));
    }

    private static LegalResearchRequest Request(DecisionResearchNeedPersistence need,int operations)=>new(
        Guid.NewGuid(),Guid.NewGuid(),need,"Example Jurisdiction",DateOnly.MaxValue,operations,5);

    private static DecisionResearchNeedPersistence Need(string proposition)=>new(
        Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,
        "Issue",proposition,"CONTROLLING_AUTHORITY","VERIFIED_AUTHORITY","Decision relevant",0.5m,0.5m,0.5m,"falsify","OPEN");

    private static WideLegalGroundingConfiguration Configuration()=>new(true,3,5,24,15,true,"https://court.example","",true,"https://gov.example","","https://ecfr.example",true,"https://lii.example");

    private sealed class StubPlanner(LegalSearchPlan plan):ILegalResearchPlanner
    {
        public LegalSearchPlan Plan(LegalResearchRequest request)=>plan with
        {
            DecisionResearchNeedId=request.ResearchNeed.DecisionResearchNeedId,
            DecisionSessionId=request.DecisionSessionId,
            TenantId=request.TenantId,
        };
    }

    private sealed class StubRetriever(IReadOnlyCollection<WideExternalKnowledgeSnippet> snippets,LegalProviderRetrievalDiagnostic diagnostic):ILegalRetriever
    {
        public Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)=>Task.FromResult(snippets);
        public Task<LegalRetrievalResult> SearchWithDiagnosticsAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)=>Task.FromResult(new LegalRetrievalResult(snippets,[diagnostic]));
    }
}
