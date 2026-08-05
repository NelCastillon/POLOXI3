using Ams.Application.Features.Platform;

namespace Ams.Application.Abstractions.Services;

public interface IRulesPlatformService
{
    Task<RulesEvaluationResponse> EvaluateAsync(EvaluateRulesRequest request, CancellationToken cancellationToken = default);
}
