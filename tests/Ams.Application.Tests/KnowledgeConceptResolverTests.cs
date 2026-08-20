using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Services;
using Ams.Knowledge.Contracts.Concepts;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeConceptResolverTests
{
    [Fact]
    public async Task ResolveAsync_StopsAfterApprovedExternalAutoMatch()
    {
        var candidate = Candidate("AUTO", .99m);
        var repository = new FakeResolutionRepository { External = [candidate] };
        var resolver = new ConceptResolver(repository, new FakePolicyProvider(new(.95m, .70m, 10)));

        var result = await resolver.ResolveAsync(Request(" Business Auto "));

        Assert.True(result.Resolved);
        Assert.Equal(candidate.ConceptId, result.Selected?.ConceptId);
        Assert.Equal(1, repository.ExternalCalls);
        Assert.Equal(0, repository.PreferredCalls);
        Assert.Equal(0, repository.FuzzyCalls);
    }

    [Fact]
    public async Task ResolveAsync_DeduplicatesByConceptAndKeepsHighestConfidence()
    {
        var conceptId = Guid.NewGuid();
        var repository = new FakeResolutionRepository
        {
            External = [new(conceptId, "AUTO", "Auto", 1, .60m, "EXTERNAL")],
            Preferred = [new(conceptId, "AUTO", "Auto", 1, .90m, "PREFERRED")]
        };
        var resolver = new ConceptResolver(repository, new FakePolicyProvider(new(.95m, .50m, 10)));

        var result = await resolver.ResolveAsync(Request("auto"));

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(.90m, candidate.Confidence);
        Assert.True(result.RequiresReview);
    }

    [Fact]
    public async Task ResolveAsync_RejectsInvalidTenantBeforeRepositoryAccess()
    {
        var repository = new FakeResolutionRepository();
        var resolver = new ConceptResolver(repository, new FakePolicyProvider(new(.95m, .70m, 10)));

        await Assert.ThrowsAnyAsync<Exception>(() => resolver.ResolveAsync(Request("Auto") with { TenantId = Guid.Empty }));

        Assert.Equal(0, repository.ExternalCalls);
    }

    private static ConceptResolutionRequest Request(string input) => new(input, null, null, null, null, null, Guid.NewGuid());
    private static ConceptCandidate Candidate(string code, decimal confidence) => new(Guid.NewGuid(), code, code, 1, confidence, "TEST");

    private sealed class FakePolicyProvider(KnowledgeResolutionPolicy policy) : IKnowledgeResolutionPolicyProvider
    {
        public Task<KnowledgeResolutionPolicy> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(policy);
    }

    private sealed class FakeResolutionRepository : IConceptResolutionRepository
    {
        public IReadOnlyCollection<ConceptCandidate> External { get; init; } = [];
        public IReadOnlyCollection<ConceptCandidate> Preferred { get; init; } = [];
        public IReadOnlyCollection<ConceptCandidate> Labels { get; init; } = [];
        public IReadOnlyCollection<ConceptCandidate> Contextual { get; init; } = [];
        public IReadOnlyCollection<ConceptCandidate> Fuzzy { get; init; } = [];
        public int ExternalCalls { get; private set; }
        public int PreferredCalls { get; private set; }
        public int FuzzyCalls { get; private set; }
        public Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedExternalCandidatesAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default) { ExternalCalls++; return Task.FromResult(External); }
        public Task<IReadOnlyCollection<ConceptCandidate>> FindPreferredLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default) { PreferredCalls++; return Task.FromResult(Preferred); }
        public Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default) => Task.FromResult(Labels);
        public Task<IReadOnlyCollection<ConceptCandidate>> FindContextualCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default) => Task.FromResult(Contextual);
        public Task<IReadOnlyCollection<ConceptCandidate>> FindFuzzyCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, int maximumCandidates, CancellationToken cancellationToken = default) { FuzzyCalls++; return Task.FromResult(Fuzzy); }
    }
}
