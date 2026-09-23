using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — GraphMaterialSignalExtractor tests (frozen slice-2 design).
//
// Locks the deterministic mapping from V2 graph to DecisionSupportSignals: assertion nodes with
// candidate/branch lineage become signals; verification state follows the incoming edges; anchor
// nodes and lineage-less assertions produce nothing; the extraction is reproducible.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class GraphMaterialSignalExtractorTests
{
    private static readonly GraphMaterialSignalExtractor Extractor = new();

    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CandidateId = Guid.NewGuid();

    private static DecisionGraphNodeDto Node(
        Guid id,
        string kind,
        decimal support = 0.7m,
        bool isEssential = false,
        string verification = "UNVERIFIED") =>
        new(id, kind, kind + "-code", "display", "statement", support, isEssential, false, verification, 0);

    private static DecisionGraphEdgeDto Edge(
        Guid source,
        Guid target,
        string relation = "SUPPORTS",
        string verification = "UNVERIFIED") =>
        new(Guid.NewGuid(), relation, "SRC", source, "TGT", target, 0.6m, 0.6m, false, false, verification, null, null);

    private static MaterialSignalExtractionContext Context(
        IReadOnlyList<DecisionGraphNodeDto> nodes,
        IReadOnlyList<DecisionGraphEdgeDto> edges) =>
        new()
        {
            SessionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Nodes = nodes,
            Edges = edges,
        };

    // A fact node depending on a branch anchor becomes a support signal with branch lineage.
    [Fact]
    public void Fact_with_branch_lineage_produces_signal()
    {
        var factId = Guid.NewGuid();
        var ctx = Context(
            [Node(factId, DecisionGraphNodeKinds.Fact), Node(BranchId, DecisionGraphNodeKinds.Branch)],
            [Edge(factId, BranchId, DecisionGraphRelations.DependsOn)]);

        var signal = Assert.Single(Extractor.Extract(ctx));
        Assert.Equal(BranchId, signal.SourceBranchId);
        Assert.Equal(DecisionSupportVerificationState.Unverified, signal.VerificationState);
    }

    // A verified incoming edge marks the signal Supported.
    [Fact]
    public void Verified_edge_marks_signal_supported()
    {
        var factId = Guid.NewGuid();
        var ctx = Context(
            [Node(factId, DecisionGraphNodeKinds.Fact), Node(CandidateId, DecisionGraphNodeKinds.Candidate)],
            [Edge(CandidateId, factId, DecisionGraphRelations.Supports, DecisionVerificationStates.Verified)]);

        var signal = Assert.Single(Extractor.Extract(ctx));
        Assert.Equal(CandidateId, signal.SourceCandidateId);
        Assert.Equal(DecisionSupportVerificationState.Supported, signal.VerificationState);
        Assert.True(signal.VerificationStrength > 0m);
    }

    // An invalidated incoming edge marks the signal Contradicted (contradiction wins).
    [Fact]
    public void Invalidated_edge_marks_signal_contradicted()
    {
        var factId = Guid.NewGuid();
        var ctx = Context(
            [Node(factId, DecisionGraphNodeKinds.Fact), Node(CandidateId, DecisionGraphNodeKinds.Candidate)],
            [
                Edge(CandidateId, factId, DecisionGraphRelations.Supports, DecisionVerificationStates.Verified),
                Edge(CandidateId, factId, DecisionGraphRelations.Contradicts, DecisionVerificationStates.Invalidated),
            ]);

        var signal = Assert.Single(Extractor.Extract(ctx));
        Assert.Equal(DecisionSupportVerificationState.Contradicted, signal.VerificationState);
    }

    // An assertion node with no candidate/branch lineage produces no signal.
    [Fact]
    public void Assertion_without_lineage_produces_no_signal()
    {
        var factId = Guid.NewGuid();
        var ctx = Context([Node(factId, DecisionGraphNodeKinds.Fact)], []);

        Assert.Empty(Extractor.Extract(ctx));
    }

    // Anchor nodes (strategy/candidate/branch) are never emitted as signals.
    [Fact]
    public void Anchor_nodes_are_not_signals()
    {
        var ctx = Context(
            [Node(BranchId, DecisionGraphNodeKinds.Branch), Node(CandidateId, DecisionGraphNodeKinds.Candidate)],
            [Edge(CandidateId, BranchId)]);

        Assert.Empty(Extractor.Extract(ctx));
    }

    // Extraction is deterministic: identical graphs yield identical signal shapes.
    [Fact]
    public void Extraction_is_deterministic()
    {
        var factId = Guid.NewGuid();
        var nodes = new[] { Node(factId, DecisionGraphNodeKinds.Fact), Node(BranchId, DecisionGraphNodeKinds.Branch) };
        var edges = new[] { Edge(factId, BranchId, DecisionGraphRelations.DependsOn) };
        var ctx = Context(nodes, edges);

        var first = Extractor.Extract(ctx);
        var second = Extractor.Extract(ctx);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].SourceBranchId, second[0].SourceBranchId);
        Assert.Equal(first[0].VerificationState, second[0].VerificationState);
        Assert.Equal(first[0].DecisionImpact, second[0].DecisionImpact);
    }
}
