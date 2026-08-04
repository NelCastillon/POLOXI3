using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Intelligence;

public interface ISemanticQueryExpander
{
    Task<SemanticQueryExpansion> ExpandAsync(Guid tenantId,string query,int maximumConcepts,CancellationToken cancellationToken=default);
}

public sealed record SemanticQueryExpansion(IReadOnlyCollection<string> Terms,IReadOnlyCollection<SemanticConceptMatchDto> Concepts);

public sealed class NullSemanticQueryExpander : ISemanticQueryExpander
{
    public Task<SemanticQueryExpansion> ExpandAsync(Guid tenantId,string query,int maximumConcepts,CancellationToken cancellationToken=default)
        => Task.FromResult(new SemanticQueryExpansion(Array.Empty<string>(),Array.Empty<SemanticConceptMatchDto>()));
}
