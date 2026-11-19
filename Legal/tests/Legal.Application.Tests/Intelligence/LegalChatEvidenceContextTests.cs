using System.Reflection;
using System.Text;
using Legal.Api.Controllers;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalChatEvidenceContextTests
{
    private static readonly MethodInfo AppendGraph = typeof(LegalChatController)
        .GetMethod("AppendEvidenceGraph", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void DocumentRoster_ReportsIntakeWithoutClaimingAtomicProof()
    {
        var versionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = new LegalDocumentDto(documentId, Guid.NewGuid(), "policy.pdf", "application/pdf", "ACTIVE",
            "INSURANCE_POLICY", null, DateTime.UnixEpoch,
            [new LegalDocumentVersionDto(versionId, 1, "hash", "storage", 123, "CLEAN", "EXTRACTED",
                "PDF", null, null, DateTime.UnixEpoch)]);
        var graph = new LegalMatterEvidenceGraphDto(document.MatterId, 1, [], [], []);
        var builder = new StringBuilder();

        AppendGraph.Invoke(null, [builder, graph, new[] { document }]);

        Assert.Contains("policy.pdf", builder.ToString());
        Assert.Contains("intake only", builder.ToString());
        Assert.Contains("atomic evidence items: 0", builder.ToString());
        Assert.Contains("malware: CLEAN", builder.ToString());
    }

    [Fact]
    public void IntakeAnchor_IsNotMisreportedAsUnlinkedAtomicEvidence()
    {
        var matterId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        LegalEvidenceGraphItemDto Evidence(string origin, string summary) => new(Guid.NewGuid(), matterId,
            versionId, null, "INSURANCE_POLICY", null, "INSURANCE_COVERAGE", summary, "RECEIVED", null,
            origin, null, null, documentId, "policy.pdf", "INSURANCE_POLICY", 1, null, null, null, null);
        var graph = new LegalMatterEvidenceGraphDto(matterId, 1,
            [Evidence("DOCUMENT_INTAKE", "Intake marker"), Evidence("EXTRACTION", "Coverage assertion")], [], []);
        var builder = new StringBuilder();

        AppendGraph.Invoke(null, [builder, graph, null]);

        Assert.Contains("atomic evidence items: 1 | intake anchors: 1", builder.ToString());
        Assert.Contains("Coverage assertion", builder.ToString());
        Assert.DoesNotContain("Intake marker\"", builder.ToString());
    }
}
