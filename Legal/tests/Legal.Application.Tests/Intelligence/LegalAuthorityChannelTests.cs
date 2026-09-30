using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Slice-3 invariants for the LegalAuthority channel: only VERIFIED legal-authority evidence is admitted,
// it targets the AuthoritySupport signal with an ESTABLISHES relation, mapping to a hierarchy node is
// deterministic and fail-soft, qualitative directness/applicability qualifiers are recorded (never
// numeric scores), and non-authority or unverified evidence is ignored. POLOXI Wide2 stays the sole engine.
public sealed class LegalAuthorityChannelTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Resolves_verified_legal_authority_to_matching_node_as_authority_support()
    {
        var breachNode = Node("Res ipsa loquitur permits an inference of negligence from the accident itself.", "PROPOSITION");
        var dutyNode = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode, dutyNode);
        var corpus = CorpusRepo(VerifiedAuthority(
            "Res ipsa loquitur permits an inference of negligence when the accident ordinarily does not occur without negligence.",
            "STANDARD"));

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(breachNode.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.LegalAuthority, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Establishes, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.AuthoritySupport, contribution.TargetSignalCode);
        Assert.True(contribution.ContributesPositiveSupport);
        Assert.Equal("Legal_EvidenceItem.LegalAuthority", contribution.Provenance.SourceTypeCode);
        Assert.Equal("STANDARD", contribution.ApplicabilityCode);
        Assert.False(string.IsNullOrWhiteSpace(contribution.DirectnessCode));
    }

    [Fact]
    public async Task Ignores_unverified_legal_authority()
    {
        var node = Node("Res ipsa loquitur permits an inference of negligence from the accident itself.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(Authority(
            "Res ipsa loquitur permits an inference of negligence when the accident ordinarily does not occur without negligence.",
            "STANDARD", LegalEvidenceStates.Proposed));

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Ignores_non_authority_evidence()
    {
        var node = Node("Res ipsa loquitur permits an inference of negligence from the accident itself.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(Evidence(
            "Res ipsa loquitur permits an inference of negligence when the accident does not ordinarily occur without negligence.",
            "STANDARD", LegalEvidenceStates.Verified, evidenceTypeCode: "WITNESS_STATEMENT"));

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Fails_soft_when_no_authoritative_hierarchy()
    {
        var corpus = CorpusRepo(VerifiedAuthority("Res ipsa loquitur permits an inference of negligence.", "STANDARD"));
        var hierarchy = HierarchyRepo();

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var context = new DecisionChannelResolveContext
        {
            TenantId = Tenant,
            UserId = User,
            DecisionMatterId = Matter,
            AuthoritativeHierarchyExecutionId = null,
        };

        var result = await channel.ResolveContributionsAsync(context);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Drops_authority_that_matches_no_node()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(VerifiedAuthority(
            "The parol evidence rule bars extrinsic terms contradicting an integrated writing.", "RULE"));

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Skips_structural_grouping_nodes()
    {
        var grouping = Node("Res ipsa loquitur inference negligence accident duty breach.", "GROUPING");
        var hierarchy = HierarchyRepo(grouping);
        var corpus = CorpusRepo(VerifiedAuthority(
            "Res ipsa loquitur inference negligence accident duty breach standard.", "STANDARD"));

        var channel = new LegalAuthorityChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DecisionChannelResolveContext Context() => new()
    {
        TenantId = Tenant,
        UserId = User,
        DecisionMatterId = Matter,
        AuthoritativeHierarchyExecutionId = Execution,
    };

    private static HierarchyNodeDto Node(string statement, string role) => new(
        HierarchyNodeId: Guid.NewGuid(),
        ParentHierarchyNodeId: null,
        Depth: 1,
        DisplayOrder: 0,
        NodeTypeCode: "ALTERNATIVE",
        NodeRoleCode: role,
        Title: null,
        Statement: statement,
        BranchStateCode: "ACTIVE",
        ContinueNarrowing: false,
        StopReasonCode: null,
        Confidence: null,
        CapabilityCode: null,
        OriginCode: "LLM_PROPOSAL");

    private static LegalEvidenceGraphItemDto VerifiedAuthority(string summary, string dimensionCode)
        => Authority(summary, dimensionCode, LegalEvidenceStates.Verified);

    private static LegalEvidenceGraphItemDto Authority(string summary, string dimensionCode, string stateCode)
        => Evidence(summary, dimensionCode, stateCode, evidenceTypeCode: "LEGAL_AUTHORITY");

    private static LegalEvidenceGraphItemDto Evidence(string summary, string dimensionCode, string stateCode, string evidenceTypeCode) => new(
        LegalEvidenceItemId: Guid.NewGuid(),
        MatterId: Matter,
        LegalDocumentVersionId: Guid.NewGuid(),
        LegalDocumentPassageId: Guid.NewGuid(),
        EvidenceTypeCode: evidenceTypeCode,
        DimensionCode: dimensionCode,
        Summary: summary,
        EvidenceStateCode: stateCode,
        Confidence: null,
        GenerationOriginCode: "DYNAMIC_LLM",
        DomainConceptCode: null,
        VerificationProfileCode: null,
        LegalDocumentId: Guid.NewGuid(),
        DocumentFileName: "authority.pdf",
        DocumentTypeCode: null,
        DocumentVersionNumber: 1,
        PageNumber: 1,
        SectionPath: null,
        PassageText: summary,
        ExtractionConfidence: null);

    private static FakeAuthorityHierarchyRepository HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeAuthorityCorpusRepository CorpusRepo(params LegalEvidenceGraphItemDto[] evidence)
        => new(evidence);

    private sealed class FakeAuthorityHierarchyRepository(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
    {
        public Task<HierarchyExecutionDetailDto?> GetExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
        {
            if (nodes is null)
                return Task.FromResult<HierarchyExecutionDetailDto?>(null);
            var summary = new HierarchyExecutionSummaryDto(
                hierarchyExecutionId, Matter, Guid.NewGuid(), 1, 1, "PRIMARY", "COMPLETED", "VALID",
                "AUTHORITATIVE", "m", "p", 1, "v", nodes.Length, 1, DateTime.UtcNow, DateTime.UtcNow, []);
            return Task.FromResult<HierarchyExecutionDetailDto?>(new HierarchyExecutionDetailDto(summary, nodes));
        }

        public Task<IReadOnlyList<HierarchyExecutionSummaryDto>> GetRunsAsync(Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<HierarchyAuthorityDto?> GetCurrentAuthorityAsync(Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<HierarchyExecutionSummaryDto> RecordExecutionAsync(Guid tenantId, Guid userId, HierarchyExecutionRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PromoteHierarchyAuthorityResult> PromoteAuthorityAsync(Guid tenantId, Guid userId, PromoteHierarchyAuthorityCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAuthorityCorpusRepository(LegalEvidenceGraphItemDto[] evidence) : StubCorpusRepository
    {
        public override Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegalMatterEvidenceGraphDto(matterId, 1, evidence, [], []));
    }
}
