using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Common.Validation;
using Ams.Knowledge.Contracts.Concepts;

namespace Ams.Knowledge.Application.Services;

public sealed class ConceptResolver : IConceptResolver
{
    private readonly IConceptResolutionRepository _repository;
    private readonly IKnowledgeResolutionPolicyProvider _policyProvider;

    public ConceptResolver(IConceptResolutionRepository repository, IKnowledgeResolutionPolicyProvider policyProvider)
    {
        _repository = repository;
        _policyProvider = policyProvider;
    }

    public async Task<ConceptResolutionResult> ResolveAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Input))
            throw new ApplicationValidationException(["TenantId and Input are required."]);

        var normalizedInput = Normalize(request.Input);
        var policy = await _policyProvider.GetAsync(request.TenantId, cancellationToken);
        if (policy.AutoResolveThreshold is < 0 or > 1 || policy.ReviewThreshold is < 0 or > 1 || policy.MaximumCandidates < 1)
            throw new InvalidOperationException("The configured Knowledge resolution policy is invalid.");

        var candidates = new Dictionary<Guid, ConceptCandidate>();
        await AddCandidatesAsync(candidates, _repository.FindApprovedExternalCandidatesAsync(request, cancellationToken));
        if (!HasAutoResolution(candidates.Values, policy.AutoResolveThreshold))
            await AddCandidatesAsync(candidates, _repository.FindPreferredLabelCandidatesAsync(request, normalizedInput, cancellationToken));
        if (!HasAutoResolution(candidates.Values, policy.AutoResolveThreshold))
            await AddCandidatesAsync(candidates, _repository.FindApprovedLabelCandidatesAsync(request, normalizedInput, cancellationToken));
        if (!HasAutoResolution(candidates.Values, policy.AutoResolveThreshold))
            await AddCandidatesAsync(candidates, _repository.FindContextualCandidatesAsync(request, normalizedInput, cancellationToken));
        if (candidates.Count == 0 || candidates.Values.Max(candidate => candidate.Confidence) < policy.ReviewThreshold)
            await AddCandidatesAsync(candidates, _repository.FindFuzzyCandidatesAsync(request, normalizedInput, policy.MaximumCandidates, cancellationToken));

        var ranked = candidates.Values
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.PreferredLabel, StringComparer.OrdinalIgnoreCase)
            .Take(policy.MaximumCandidates)
            .ToArray();
        var selected = ranked.FirstOrDefault(candidate => candidate.Confidence >= policy.AutoResolveThreshold);
        return new ConceptResolutionResult(selected is not null, selected, ranked, selected is null && ranked.Length > 0);
    }

    private static async Task AddCandidatesAsync(Dictionary<Guid, ConceptCandidate> candidates, Task<IReadOnlyCollection<ConceptCandidate>> candidateTask)
    {
        foreach (var candidate in await candidateTask)
        {
            if (!candidates.TryGetValue(candidate.ConceptId, out var existing) || candidate.Confidence > existing.Confidence)
                candidates[candidate.ConceptId] = candidate;
        }
    }

    private static bool HasAutoResolution(IEnumerable<ConceptCandidate> candidates, decimal threshold)
        => candidates.Any(candidate => candidate.Confidence >= threshold);

    private static string Normalize(string input)
        => string.Join(' ', input.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
