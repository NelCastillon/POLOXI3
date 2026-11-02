using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Services;

// Enterprise Search Quality Indicator scorer. Computes a per-result quality assessment (A-E grade +
// composite score + weighted factor breakdown + model-appropriateness suggestions) from a completed
// WideSearchResponse using DB-backed rubric configuration (migration 0475). The deterministic score is
// authoritative; an optional LLM pass may only refine suggestion phrasing. Implementations must be
// fail-soft: any error returns null so the critical search path and capture are never broken.
public interface ISearchQualityScorer
{
    Task<SearchQualityAssessmentDto?> AssessAsync(WideSearchResponse response,Guid tenantId,string? requestedModelCode,string? contextCode,CancellationToken cancellationToken=default);
}
