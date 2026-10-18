using System.Linq;
using System.Text.Json;
using Legal.Application;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Covers the Candidate x Branch assessment contract: the matrix response may represent an insufficient
// cell as evidenceScore=null (the schema's permitted missing-value convention), the default prompt
// instructs the model to use that convention instead of inventing support, and the structured-output
// schema admits the null value so the router does not reject a truthful response.
public sealed class WideCandidateMatrixAssessmentContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Assessment_Proposal_Deserializes_Null_Cells_Without_Inventing_Scores()
    {
        const string json = """
        {"candidates":[{"name":"Dismissal","detail":null,"violatesConstraint":false,"constraintViolationReason":null,"isEntityOfRequestedKind":true,
          "branchScores":[{"branchDisplayName":"Liability","evidenceScore":0.7},{"branchDisplayName":"Damages","evidenceScore":null}]}]}
        """;

        var proposal = JsonSerializer.Deserialize<WideCandidateAssessmentProposal>(json, Options);

        var cells = Assert.Single(proposal!.Candidates).BranchScores!;
        Assert.Equal(0.7m, cells.Single(c => c.BranchDisplayName == "Liability").EvidenceScore);
        Assert.Null(cells.Single(c => c.BranchDisplayName == "Damages").EvidenceScore);
        var numeric = cells.Where(c => c.EvidenceScore is not null).Select(c => new WideCandidateBranchEvidence(c.BranchDisplayName, c.EvidenceScore!.Value)).ToArray();
        Assert.Single(numeric);
    }

    [Fact]
    public void Default_Matrix_Prompt_Requires_Supplied_Material_And_Null_For_Insufficient()
    {
        var prompt = IntelligencePromptDefaults.WideCandidateMatrix;

        Assert.Contains("evidenceScore=null", prompt);
        Assert.Contains("do not invent support", prompt);
        Assert.Contains("CONTRADICTED", prompt);
        Assert.DoesNotContain("based on your knowledge", prompt);
        Assert.DoesNotContain("never assign identical scores", prompt);
    }

    [Fact]
    public void Scoring_Schema_Admits_Null_Evidence_Score()
    {
        var field = typeof(IntelligenceWide2Service).GetField("CandidateScoringSchema", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var schema = (string)field!.GetValue(null)!;

        using var document = JsonDocument.Parse(schema);
        var types = document.RootElement.GetProperty("properties").GetProperty("candidates").GetProperty("items").GetProperty("properties")
            .GetProperty("branchScores").GetProperty("items").GetProperty("properties").GetProperty("evidenceScore").GetProperty("type");
        Assert.Contains(types.EnumerateArray().Select(t => t.GetString()), t => t == "null");
    }
}
