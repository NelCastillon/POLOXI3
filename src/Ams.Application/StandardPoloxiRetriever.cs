using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;

namespace Ams.Application;

// Default IAdaptiveRetriever: executes the approved deterministic capability search for one branch
// and, when the budget allows and the primary attempt yields nothing, performs exactly one
// deterministic alternate-approved-term retry. Reformulation never leaves the admission gate: the
// alternate term must be one of the capability's approved terms, preferring a term grounded in the
// user's query or the branch condition. No LLM calls; attempt count is bounded by MaximumAttempts.
public sealed class StandardPoloxiRetriever(IIntelligenceRepository repository):IAdaptiveRetriever
{
    public async Task<PoloxiEvidencePacket> RetrieveAsync(PoloxiEvidenceRequest request,CancellationToken cancellationToken=default)
    {
        var evidence=await repository.ExecutePoloxiBranchAsync(request.Search,request.Branch,request.Capability,request.MaximumResults,cancellationToken);
        if(evidence.Count>0||request.MaximumAttempts<=1)return new(evidence,1,null);
        var alternateTerm=SelectAlternateApprovedTerm(request.Branch,request.Capability,request.Search.Query);
        if(string.IsNullOrWhiteSpace(alternateTerm))return new(evidence,1,null);
        var retried=await repository.ExecutePoloxiBranchAsync(request.Search,request.Branch with{SearchText=alternateTerm},request.Capability,request.MaximumResults,cancellationToken);
        return new(retried,2,alternateTerm);
    }

    private static string? SelectAlternateApprovedTerm(PoloxiBranchRecord branch,PoloxiCapabilityDto capability,string query)
    {
        bool Differs(string term)=>!term.Equals(branch.SearchText,StringComparison.OrdinalIgnoreCase);
        return capability.ApprovedTerms.FirstOrDefault(term=>Differs(term)&&(query.Contains(term,StringComparison.OrdinalIgnoreCase)||branch.ProposedCondition.Contains(term,StringComparison.OrdinalIgnoreCase)))
            ??capability.ApprovedTerms.FirstOrDefault(Differs);
    }
}
