using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Env-gated live baseline recorder ─────────────────────────────────────────────────────────
// This is a RECORDER, not an assertion test. It is skipped unless POLOXI_BASELINE_RECORD=1 and a
// live IIntelligenceWideService can be constructed by the caller. Normal unit/CI runs never touch
// it (no DB, no network). To capture fixtures:
//
//   1. Wire a live IIntelligenceWideService (real DI: repositories, provider router, retrievers).
//      This project intentionally has NO infrastructure reference, so the composition below is a
//      placeholder the operator fills in for a one-off recording session.
//   2. Set env vars:
//        POLOXI_BASELINE_RECORD=1
//        POLOXI_BASELINE_TENANT=<tenant guid>
//        POLOXI_BASELINE_USER=<user guid>
//        POLOXI_BASELINE_DIR=<abs path to Legal/tests/Legal.Application.Tests/Fixtures/Baseline>
//   3. Run this single test. It writes <slug>.golden.json + <slug>.perf.json per query.
//   4. Commit the generated fixtures. Phase 1 then diffs the golden files.
public sealed class WideBaselineRecordingTests
{
    private static readonly string[] BaselineQueries =
    [
        "Which carriers offer cyber insurance for early-stage fintech startups?",
        "What statute governs data breach notification timelines in California?",
        "Compare general liability options for a small construction contractor.",
    ];

    [Fact]
    public async Task RecordBaselineFixtures()
    {
        if (Environment.GetEnvironmentVariable("POLOXI_BASELINE_RECORD") != "1")
        {
            // Skipped by design in normal runs — this recorder requires a live environment.
            return;
        }

        var dir = Environment.GetEnvironmentVariable("POLOXI_BASELINE_DIR")
            ?? throw new InvalidOperationException("POLOXI_BASELINE_DIR is required to record baseline fixtures.");
        var tenantId = Guid.Parse(Environment.GetEnvironmentVariable("POLOXI_BASELINE_TENANT")!);
        var userId = Guid.Parse(Environment.GetEnvironmentVariable("POLOXI_BASELINE_USER")!);

        IIntelligenceWideService service = CreateLiveService();

        foreach (var query in BaselineQueries)
        {
            var request = new WideSearchRequest(tenantId, userId, query,
                MaximumResults: 25, CorrelationId: "baseline-" + Guid.NewGuid().ToString("N"));
            var response = await service.SearchDynamicAsync(request, CancellationToken.None);
            var (goldenPath, perfPath) = WideBaselineFixtureHarness.Save(dir, response, "poloxi-current");
            Assert.True(File.Exists(goldenPath));
            Assert.True(File.Exists(perfPath));
        }
    }

    // Placeholder: the recording operator wires the real, fully-configured Wide service here for a
    // one-off session. Kept as a throwing stub so the recorder fails loudly if run without setup,
    // while the test project stays free of an infrastructure dependency for normal unit runs.
    private static IIntelligenceWideService CreateLiveService() =>
        throw new NotImplementedException(
            "Wire a live IIntelligenceWideService (real DI) before recording baseline fixtures. " +
            "See the class remarks for the required setup.");
}
