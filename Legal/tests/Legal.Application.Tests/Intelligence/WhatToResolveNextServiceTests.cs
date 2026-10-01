using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Verifies that What To Resolve Next is a THIN PROJECTION over the existing proposition VIV / LegalADV
// frontier: ranking comes from the existing InformationValue ordering, the four states stay distinct,
// unknown metrics (FlipPotential) stay null and never become zero, and no new score is computed.
public sealed class WhatToResolveNextServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static WhatToResolveNextService CreateService(IMatterPropositionInformationValueService iv)
        => new(iv, new EmptyGraphCorpusRepository(), new NoMatterDecisionRepository(), new NoPackResolver());

    [Fact]
    public async Task Calculated_RanksTargetsByExistingInformationValue()
    {
        var low = Prop("P-low", "defendant was speeding", LegalFactStates.Alleged, informationValue: 0.30m, decisionImpact: 0.80m);
        var high = Prop("P-high", "defendant had an unobstructed view before impact", LegalFactStates.Alleged, informationValue: 0.90m, decisionImpact: 0.80m);
        var mid = Prop("P-mid", "the road was wet", LegalFactStates.Alleged, informationValue: 0.60m, decisionImpact: 0.80m);
        var service = CreateService(new FakeIvService(new MatterPropositionInformationValueResult(Matter, [low, high, mid], false)));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        Assert.Equal(ResolutionTargetState.Calculated, result.State);
        // Ranking is the EXISTING InformationValue ordering (descending), not a new score.
        Assert.Equal([0.90, 0.60, 0.30], result.Targets.Select(t => t.InformationValue));
        // LegalADV is the existing IV/maxIV normalization, not a new formula. Top target normalizes to 1.0.
        Assert.Equal(1.0, result.Targets[0].LegalAdv);
        Assert.Equal(0.90, result.Targets[0].InformationValue);
    }

    [Fact]
    public async Task UnknownFlipPotential_StaysNull_NeverZero()
    {
        var p = Prop("P1", "a material fact", LegalFactStates.Alleged, informationValue: 0.50m, decisionImpact: 0.50m);
        var service = CreateService(new FakeIvService(new MatterPropositionInformationValueResult(Matter, [p], false)));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        var target = Assert.Single(result.Targets);
        Assert.Null(target.FlipPotential);
    }

    [Fact]
    public async Task NoScoredPropositions_IsNotCalculated_NotNoneRequired()
    {
        var service = CreateService(new FakeIvService(new MatterPropositionInformationValueResult(Matter, [], false)));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        Assert.Equal(ResolutionTargetState.NotCalculated, result.State);
        Assert.NotEqual(ResolutionTargetState.NoneRequired, result.State);
        Assert.Empty(result.Targets);
    }

    [Fact]
    public async Task AllResolved_IsNoneRequired_Distinct()
    {
        var resolved = Prop("P1", "an established fact", LegalFactStates.Established, informationValue: 0.90m, decisionImpact: 0.90m);
        var service = CreateService(new FakeIvService(new MatterPropositionInformationValueResult(Matter, [resolved], false)));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        Assert.Equal(ResolutionTargetState.NoneRequired, result.State);
        Assert.NotEqual(ResolutionTargetState.NotCalculated, result.State);
        Assert.Empty(result.Targets);
    }

    [Fact]
    public async Task ScoringFailure_IsBlocked_WithReason()
    {
        var service = CreateService(new ThrowingIvService("prioritizer unavailable"));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        Assert.Equal(ResolutionTargetState.Blocked, result.State);
        Assert.Contains("prioritizer unavailable", result.Reason);
        Assert.Empty(result.Targets);
    }

    [Fact]
    public async Task BelowMaterialityFloor_IsNoneRequired()
    {
        var immaterial = Prop("P1", "a trivial fact", LegalFactStates.Alleged, informationValue: 0.90m, decisionImpact: 0.10m);
        var service = CreateService(new FakeIvService(new MatterPropositionInformationValueResult(Matter, [immaterial], false)));

        var result = await service.GetAsync(Tenant, Matter, Session, null);

        Assert.Equal(ResolutionTargetState.NoneRequired, result.State);
    }

    private static MatterPropositionInformationValue Prop(
        string id, string text, string factState, decimal informationValue, decimal decisionImpact)
        => new(
            DeterministicGuid(id),
            text,
            factState,
            "UNVERIFIED",
            informationValue,
            IsEssential: false,
            IsExecutable: true,
            Materiality: decisionImpact,
            Uncertainty: 0.5m,
            DecisionImpact: decisionImpact,
            Discrimination: 0.5m,
            RedundancyPenalty: 0m);

    // Stable GUID per logical id so PropositionId round-trips to a readable value in assertions.
    private static Guid DeterministicGuid(string id)
    {
        var bytes = new byte[16];
        var raw = System.Text.Encoding.UTF8.GetBytes(id);
        Array.Copy(raw, bytes, Math.Min(raw.Length, 16));
        return new Guid(bytes);
    }

    private sealed class FakeIvService(MatterPropositionInformationValueResult result) : IMatterPropositionInformationValueService
    {
        public Task<MatterPropositionInformationValueResult> ScoreAsync(
            Guid tenantId, Guid matterId, Guid decisionSessionId, Guid? actorUserId, bool persist = true, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class ThrowingIvService(string message) : IMatterPropositionInformationValueService
    {
        public Task<MatterPropositionInformationValueResult> ScoreAsync(
            Guid tenantId, Guid matterId, Guid decisionSessionId, Guid? actorUserId, bool persist = true, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(message);
    }

    private sealed class EmptyGraphCorpusRepository : StubCorpusRepository
    {
        public override Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegalMatterEvidenceGraphDto(matterId, 0, [], [], []));
    }

    private sealed class NoMatterDecisionRepository : StubDecisionRepository
    {
        public override Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<DecisionMatterDto?>(null);
    }

    private sealed class NoPackResolver : IDomainPackResolver
    {
        public Task<ResolvedDomainPack> ResolveAsync(Guid tenantId, string? packCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolvedDomainPack(packCode ?? string.Empty, string.Empty, [], [], [], [], []));
    }
}
