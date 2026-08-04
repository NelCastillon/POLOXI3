using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.Intelligence;
using Ams.Knowledge.Contracts.Concepts;

namespace Ams.Api.Services;

public sealed class KnowledgeSemanticQueryExpander(IConceptResolver resolver,ILogger<KnowledgeSemanticQueryExpander> logger):ISemanticQueryExpander
{
    public async Task<SemanticQueryExpansion> ExpandAsync(Guid tenantId,string query,int maximumConcepts,CancellationToken cancellationToken=default)
    {
        var tokens=query.Split([' ',',',';','/','-'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Length>2).Take(12).ToArray();var phrases=new HashSet<string>(StringComparer.OrdinalIgnoreCase){query.Trim()};
        for(var length=Math.Min(3,tokens.Length);length>=1;length--)for(var index=0;index+length<=tokens.Length;index++)phrases.Add(string.Join(' ',tokens.Skip(index).Take(length)));
        var candidates=new Dictionary<Guid,SemanticConceptMatchDto>();
        foreach(var phrase in phrases.Take(30))
        {
            try
            {
                var result=await resolver.ResolveAsync(new(phrase,null,null,null,null,null,tenantId),cancellationToken);
                foreach(var candidate in result.Candidates)if(!candidates.TryGetValue(candidate.ConceptId,out var existing)||candidate.Confidence>existing.Score)candidates[candidate.ConceptId]=new(candidate.ConceptId,candidate.ConceptCode,candidate.PreferredLabel,candidate.VersionNumber,candidate.Confidence,candidate.MatchReasonCode);
            }
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
            catch(Exception ex){logger.LogWarning(ex,"Knowledge expansion failed for query phrase {Phrase}; hybrid search will continue with available terms.",phrase);}
        }
        var ranked=candidates.Values.OrderByDescending(x=>x.Score).ThenBy(x=>x.PreferredLabel).Take(Math.Clamp(maximumConcepts,1,50)).ToArray();var terms=ranked.SelectMany(x=>new[]{x.PreferredLabel,x.ConceptCode.Replace('_',' ')}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();return new(terms,ranked);
    }
}
