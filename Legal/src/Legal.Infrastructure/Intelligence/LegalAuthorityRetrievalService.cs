using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;

namespace Legal.Infrastructure.Intelligence;

public sealed class LegalAuthorityRetrievalService(
    ILegalResearchPlanner planner,
    ILegalRetriever retriever):ILegalAuthorityRetrievalService
{
    public async Task<LegalAuthorityRetrievalResult> RetrieveAsync(
        LegalResearchRequest request,
        WideLegalGroundingConfiguration configuration,
        CancellationToken cancellationToken=default)
    {
        LegalSearchPlan plan;
        try
        {
            plan=planner.Plan(request);
        }
        catch(ArgumentException exception)
        {
            var invalidPlan=new LegalSearchPlan(Guid.Empty,request.ResearchNeed.DecisionResearchNeedId,request.DecisionSessionId,request.TenantId,
                request.ResearchNeed.PropositionToResolve??string.Empty,request.Jurisdiction,request.AuthorityCutoffDate,[]);
            return new(invalidPlan,request.ResearchNeed.DecisionResearchNeedId,LegalRetrievalOutcome.InvalidQuery,[],
                [new(Guid.NewGuid(),Guid.Empty,Guid.Empty,"PLANNER",LegalRetrievalOutcome.InvalidQuery,0,0,exception.Message,0)]);
        }

        var attempts=new List<LegalProviderAttempt>();
        var authorities=new Dictionary<string,AuthorityAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach(var operation in plan.Operations.Take(request.MaximumOperations))
        {
            var timer=Stopwatch.StartNew();
            LegalRetrievalResult result;
            try
            {
                result=await retriever.SearchScopedAsync(
                    new(operation.Query,operation.AuthorityKind,plan.AuthorityScope),
                    configuration,
                    cancellationToken);
            }
            catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
            {
                timer.Stop();
                attempts.Add(new(Guid.NewGuid(),plan.LegalSearchPlanId,operation.LegalSearchOperationId,"AGGREGATE",
                    LegalRetrievalOutcome.ProviderFailure,0,0,exception.Message,timer.ElapsedMilliseconds)
                {
                    OperationKind=operation.Kind,
                    Query=operation.Query,
                    RecoveryActionCode=RecoveryAction(LegalRetrievalOutcome.ProviderFailure,operation,plan),
                });
                continue;
            }
            timer.Stop();
            foreach(var diagnostic in result.Providers)
            {
                var providerOutcome=MapOutcome(diagnostic);
                attempts.Add(new(Guid.NewGuid(),plan.LegalSearchPlanId,operation.LegalSearchOperationId,diagnostic.ProviderCode,
                    providerOutcome,diagnostic.RawResultCount,diagnostic.ReturnedCount,diagnostic.Detail,timer.ElapsedMilliseconds)
                {
                    OperationKind=operation.Kind,
                    Query=operation.Query,
                    RecoveryActionCode=RecoveryAction(providerOutcome,operation,plan),
                });
            }

            foreach(var snippet in result.Snippets)
            {
                var identity=CreateIdentity(snippet);
                if(!authorities.TryGetValue(identity,out var accumulator))
                {
                    accumulator=new(identity,snippet,plan);
                    authorities.Add(identity,accumulator);
                }
                accumulator.OperationIds.Add(operation.LegalSearchOperationId);
            }
            // Keep a bounded candidate pool across planned operations. Do not stop at the first provider
            // page: early provider order is not evidence that a passage supports the atomic proposition.
            if(authorities.Count>=Math.Clamp(request.MaximumAuthorities*4,request.MaximumAuthorities,50))break;
        }

        var normalized=authorities.Values
            .Select(value=>value.ToAuthority())
            .Select(authority=>authority with
            {
                PropositionSelectionScore=ScorePropositionSupport(plan.Proposition,authority.Title,authority.Passage),
            })
            .OrderByDescending(authority=>authority.PropositionSelectionScore)
            .ThenByDescending(authority=>authority.ProviderIdentityVerified)
            .ThenBy(authority=>authority.AuthorityIdentity,StringComparer.Ordinal)
            .Take(request.MaximumAuthorities)
            .Select((authority,index)=>authority with{PropositionSelectionRank=index+1})
            .ToList();
        var outcome=normalized.Count>0?LegalRetrievalOutcome.ResultsFound:AggregateOutcome(attempts);
        return new(plan,plan.DecisionResearchNeedId,outcome,normalized,attempts);
    }

    private static LegalRetrievalOutcome MapOutcome(LegalProviderRetrievalDiagnostic diagnostic)
    {
        var code=diagnostic.OutcomeCode.ToUpperInvariant();
        if(diagnostic.ReturnedCount>0||code is "SUCCEEDED" or "RESULTS_FOUND")return LegalRetrievalOutcome.ResultsFound;
        if(code.Contains("ACCESS_DENIED")||code.Contains("UNAUTHORIZED")||code.Contains("FORBIDDEN"))return LegalRetrievalOutcome.AccessDenied;
        if(code.Contains("COVERAGE")||code.Contains("DISABLED")||code.Contains("TENANT_UNAVAILABLE"))return LegalRetrievalOutcome.CoverageGap;
        if(code.Contains("REQUIRED_")&&code.Contains("MISSING"))return LegalRetrievalOutcome.RequiredScopeMissing;
        if(code.Contains("SCOPE_UNSUPPORTED"))return LegalRetrievalOutcome.ScopeUnsupported;
        if(code.Contains("INVALID"))return LegalRetrievalOutcome.InvalidQuery;
        if(code.Contains("PARSE")||code.Contains("EXTRACT")||code.Contains("MALFORMED"))return LegalRetrievalOutcome.ParsingFailure;
        if(code.Contains("FILTER")||diagnostic.RawResultCount>0&&diagnostic.ReturnedCount==0)return LegalRetrievalOutcome.FilteredOut;
        if(code.Contains("FAIL")||code.Contains("ERROR")||code.Contains("TIMEOUT"))return LegalRetrievalOutcome.ProviderFailure;
        return LegalRetrievalOutcome.NoResults;
    }

    private static LegalRetrievalOutcome AggregateOutcome(IReadOnlyCollection<LegalProviderAttempt> attempts)
    {
        if(attempts.Count==0)return LegalRetrievalOutcome.CoverageGap;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.AccessDenied))return LegalRetrievalOutcome.AccessDenied;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.ProviderFailure))return LegalRetrievalOutcome.ProviderFailure;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.ParsingFailure))return LegalRetrievalOutcome.ParsingFailure;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.FilteredOut))return LegalRetrievalOutcome.FilteredOut;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.RequiredScopeMissing))return LegalRetrievalOutcome.RequiredScopeMissing;
        if(attempts.Any(attempt=>attempt.Outcome==LegalRetrievalOutcome.ScopeUnsupported))return LegalRetrievalOutcome.ScopeUnsupported;
        if(attempts.All(attempt=>attempt.Outcome==LegalRetrievalOutcome.CoverageGap))return LegalRetrievalOutcome.CoverageGap;
        if(attempts.All(attempt=>attempt.Outcome==LegalRetrievalOutcome.InvalidQuery))return LegalRetrievalOutcome.InvalidQuery;
        return LegalRetrievalOutcome.NoResults;
    }

    private static string RecoveryAction(
        LegalRetrievalOutcome outcome,
        LegalSearchOperation operation,
        LegalSearchPlan plan)
    {
        if(outcome==LegalRetrievalOutcome.ResultsFound)return "STOP_IF_AUTHORITY_BUDGET_MET";
        if(outcome is LegalRetrievalOutcome.AccessDenied or LegalRetrievalOutcome.CoverageGap)return "TRY_NEXT_CONFIGURED_PROVIDER_OR_OPERATION";
        if(outcome is LegalRetrievalOutcome.ProviderFailure or LegalRetrievalOutcome.ParsingFailure)return "TRY_NEXT_BOUNDED_OPERATION";
        if(outcome is LegalRetrievalOutcome.NoResults or LegalRetrievalOutcome.FilteredOut)
            return operation.Sequence<plan.Operations.Count?"BROADEN_WITH_NEXT_PLANNED_OPERATION":"BOUNDED_SEARCH_EXHAUSTED";
        return "STOP_INVALID_QUERY";
    }

    private static string CreateIdentity(WideExternalKnowledgeSnippet snippet)
    {
        if(Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri))
        {
            var canonical=$"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/').ToLowerInvariant()}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }
        var value=Regex.Replace($"{snippet.Title}|{snippet.Snippet}",@"\s+"," ").Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    internal static decimal ScorePropositionSupport(string proposition,string title,string passage)
    {
        var propositionTerms=Terms(proposition);
        if(propositionTerms.Count==0)return 0m;
        var titleTerms=Terms(title);
        var passageTerms=Terms(passage);
        var titleOverlap=propositionTerms.Count(term=>titleTerms.Contains(term));
        var passageOverlap=propositionTerms.Count(term=>passageTerms.Contains(term));
        var coverage=(decimal)passageOverlap/propositionTerms.Count;
        var titleCoverage=(decimal)titleOverlap/propositionTerms.Count;
        var phrase=Normalize(passage).Contains(Normalize(proposition),StringComparison.OrdinalIgnoreCase)?1m:0m;
        return Math.Round(Math.Min(1m,coverage*0.75m+titleCoverage*0.15m+phrase*0.10m),6);
    }

    private static HashSet<string> Terms(string value)=>Regex.Matches(Normalize(value),@"[a-z0-9]{3,}",RegexOptions.IgnoreCase)
        .Select(match=>match.Value.ToUpperInvariant())
        .Where(term=>term is not("THE" or "AND" or "FOR" or "THAT" or "WITH" or "UNDER" or "FROM" or "THIS" or "WERE" or "WAS" or "ARE"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value)=>Regex.Replace(value??string.Empty,@"\s+"," ").Trim();

    private sealed class AuthorityAccumulator(string identity,WideExternalKnowledgeSnippet snippet,LegalSearchPlan plan)
    {
        public HashSet<Guid> OperationIds{get;}=[];
        public NormalizedLegalAuthority ToAuthority()=>new(identity,snippet.Url,snippet.Title,snippet.Snippet,
            snippet.Jurisdiction,null,snippet.AuthorityKind,snippet.SourceProvider,snippet.SourceVersion,
            snippet.ProviderIdentityVerified,OperationIds.ToList())
        {
            AtomicPropositionId=plan.AtomicPropositionId,
            LegalSearchPlanId=plan.LegalSearchPlanId,
            PassageIdentity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                Regex.Replace($"{snippet.Url}|{snippet.Snippet}",@"\s+"," ").Trim().ToUpperInvariant()))),
        };
    }
}
