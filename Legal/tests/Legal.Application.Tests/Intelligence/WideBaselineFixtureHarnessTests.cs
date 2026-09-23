using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Validates the fixture harness itself deterministically — no live run required. Ensures the
// golden-master projection is stable across volatile fields (GUIDs, timings) so it can serve as a
// reliable Phase 1 regression anchor.
public sealed class WideBaselineFixtureHarnessTests
{
    private static WideSearchResponse Response(Guid executionId, long durationMs, int llmCalls) =>
        new(
            WideExecutionId: executionId,
            Query: "Which carriers offer cyber for fintech startups?",
            StatusCode: "COMPLETED",
            TerminationReasonCode: "CONVERGED",
            DepthReached: 2,
            LlmCallCount: llmCalls,
            FinalConfidence: .8123m,
            AnswerVerificationCode: "VERIFIED",
            FinalAnswer: "Some answer.",
            Branches:
            [
                new(Guid.NewGuid(), null, 1, "B1", "Cyber", "Cyber interpretation",
                    null, null, "GROUNDED", 3, .7m, false, null, false, null, 1)
                {
                    BranchStateCode = WideBranchStates.Active,
                },
            ],
            Evidence: [],
            SuggestedActions: [],
            DurationMilliseconds: durationMs)
        {
            FinalNarrowingTrend = WideNarrowingTrends.Converged,
        };

    [Fact]
    public void GoldenMaster_IsIdentical_AcrossVolatileFieldChanges()
    {
        var a = WideBaselineFixtureHarness.BuildGoldenMaster(Response(Guid.NewGuid(), durationMs: 1200, llmCalls: 5));
        var b = WideBaselineFixtureHarness.BuildGoldenMaster(Response(Guid.NewGuid(), durationMs: 9800, llmCalls: 42));

        Assert.Equal(a, b);
    }

    [Fact]
    public void GoldenMaster_ExcludesTimingAndCallCounts()
    {
        var golden = WideBaselineFixtureHarness.BuildGoldenMaster(Response(Guid.NewGuid(), 1200, 5));

        Assert.DoesNotContain("durationMilliseconds", golden, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("llmCallCount", golden, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wideExecutionId", golden, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PerformanceRecord_CapturesTimingAndCallCounts()
    {
        var perf = WideBaselineFixtureHarness.BuildPerformanceRecord(Response(Guid.NewGuid(), 1200, 5), "poloxi");

        Assert.Contains("1200", perf);
        Assert.Contains("\"llmCallCount\": 5", perf);
        Assert.Contains("poloxi", perf);
    }

    [Fact]
    public void Save_WritesGoldenAndPerfArtifacts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poloxi-fixtures-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (goldenPath, perfPath) = WideBaselineFixtureHarness.Save(dir, Response(Guid.NewGuid(), 1200, 5), "poloxi");

            Assert.True(File.Exists(goldenPath));
            Assert.True(File.Exists(perfPath));
            Assert.EndsWith(".golden.json", goldenPath);
            Assert.EndsWith(".perf.json", perfPath);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
