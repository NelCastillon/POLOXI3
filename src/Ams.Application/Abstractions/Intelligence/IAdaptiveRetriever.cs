using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Intelligence;

// GRIP-style tactical retrieval controller: POLOXI (the strategic decision controller) issues a
// budgeted evidence request for one branch; the retriever decides how the evidence is obtained
// (primary capability search, deterministic reformulation retry, future agentic strategies) and
// returns an evidence packet. Implementations must never lower admission gates: every retrieval
// must stay inside approved deterministic capabilities and their approved terms, and must respect
// the request budget (MaximumAttempts) so worst-case cost stays bounded.
public interface IAdaptiveRetriever
{
    Task<PoloxiEvidencePacket> RetrieveAsync(PoloxiEvidenceRequest request,CancellationToken cancellationToken=default);
}
