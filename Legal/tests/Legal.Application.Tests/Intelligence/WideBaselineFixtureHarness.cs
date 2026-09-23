using System.Text;
using System.Text.Json;
using Legal.Application.Features.Intelligence;

namespace Legal.Application.Tests.Intelligence;

// ── Phase 0 end-to-end fixture harness ───────────────────────────────────────────────────────
// Turns a live WideSearchResponse into two committed artifacts:
//   1. A STABLE golden-master snapshot (volatile fields — GUIDs, timings — stripped) so hierarchy
//      shape, candidate ranking, and convergence outcome can be diffed across a refactor.
//   2. A PERFORMANCE record (timing / LLM-call / depth) for the section-40 benchmark comparison.
// The harness is pure/deterministic; capturing the live response is the caller's job (see
// WideBaselineRecordingTests, which is env-gated and skipped in normal CI/unit runs).
public static class WideBaselineFixtureHarness
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Deterministic, refactor-stable projection of a Wide run. Excludes GUIDs and any wall-clock
    // value so the same query produces byte-identical output before and after a Phase 1 extraction.
    public static string BuildGoldenMaster(WideSearchResponse response)
    {
        var snapshot = new
        {
            response.Query,
            response.StatusCode,
            response.TerminationReasonCode,
            response.DepthReached,
            response.AnswerVerificationCode,
            response.FinalNarrowingTrend,
            HasFinalAnswer = !string.IsNullOrWhiteSpace(response.FinalAnswer),
            Branches = response.Branches
                .OrderBy(b => b.LevelNumber).ThenBy(b => b.SortOrder).ThenBy(b => b.DisplayName, StringComparer.Ordinal)
                .Select(b => new
                {
                    b.LevelNumber,
                    b.DisplayName,
                    b.BranchStateCode,
                    b.SemanticTypeCode,
                    b.BranchRoleCode,
                    b.GroundingStatusCode,
                    b.ContinueNarrowing,
                    b.IsEliminated,
                    b.EliminationReason,
                })
                .ToArray(),
            NarrowingTrends = response.NarrowingIterations
                .Select(i => i.TrendCode)
                .ToArray(),
        };
        return JsonSerializer.Serialize(snapshot, Json);
    }

    // Volatile performance metrics kept separate from the golden-master so they never cause a
    // characterization diff to fail. Used for the Raw-LLM / LLM+search / POLOXI / POLOXI-Research
    // benchmark table.
    public static string BuildPerformanceRecord(WideSearchResponse response, string label)
    {
        var perf = new
        {
            Label = label,
            CapturedUtc = DateTime.UtcNow,
            response.Query,
            response.StatusCode,
            response.DepthReached,
            response.LlmCallCount,
            FinalConfidence = decimal.Round(response.FinalConfidence, 4),
            response.DurationMilliseconds,
            BranchCount = response.Branches.Count,
            EvidenceCount = response.Evidence.Count,
        };
        return JsonSerializer.Serialize(perf, Json);
    }

    // Writes both artifacts under the committed fixtures directory, keyed by a slug of the query.
    public static (string GoldenPath, string PerfPath) Save(string fixturesDirectory, WideSearchResponse response, string label)
    {
        Directory.CreateDirectory(fixturesDirectory);
        var slug = Slug(response.Query);
        var goldenPath = Path.Combine(fixturesDirectory, $"{slug}.golden.json");
        var perfPath = Path.Combine(fixturesDirectory, $"{slug}.perf.json");
        File.WriteAllText(goldenPath, BuildGoldenMaster(response), Encoding.UTF8);
        File.WriteAllText(perfPath, BuildPerformanceRecord(response, label), Encoding.UTF8);
        return (goldenPath, perfPath);
    }

    private static string Slug(string query)
    {
        var sb = new StringBuilder(query.Length);
        foreach (var ch in query.Trim().ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Length > 80 ? slug[..80].Trim('-') : slug;
    }
}
