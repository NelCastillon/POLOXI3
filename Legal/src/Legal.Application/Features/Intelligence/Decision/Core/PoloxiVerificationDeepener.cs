namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class DisabledPoloxiVerificationDeepener : IPoloxiVerificationDeepener
{
    public Task<SemanticVerificationResult> DeepenAsync(
        PoloxiVerificationContract contract,
        SemanticVerificationResult current,
        CancellationToken cancellationToken = default) => Task.FromResult(current);
}