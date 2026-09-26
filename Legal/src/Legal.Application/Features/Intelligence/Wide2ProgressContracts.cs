namespace Legal.Application.Features.Intelligence;

// ── Real-time Wide2 progress feed ─────────────────────────────────────────────────────────────
// The Wide2 pipeline runs on a background task (Wide2SearchOperationStore) with a client-supplied
// CorrelationId. As the hierarchy is built and each level is scored, the service publishes a
// running snapshot of the four cockpit KPIs so the Blazor console can fill/auto-increment them in
// real time while processing. All interim values are PROVISIONAL until Phase == "COMPLETED"; the
// UI must clearly label them as "analyzing" so they are never mistaken for the final decision.

// A single provisional (or final) snapshot of the four Decision console KPIs plus a phase label.
public sealed record Wide2ProgressUpdate(
    string CorrelationId,
    string Phase,
    int CompetingOutcomes,
    int IdentifiedFactors,
    int AdmittedEvidence,
    string DecisionReadiness,
    bool IsProvisional,
    string? Message = null);

// Publisher abstraction implemented by the API host over SignalR. Injected into the Wide2 service.
// Publishing must never throw into the pipeline: a broken/absent transport must not affect results.
public interface IWide2ProgressPublisher
{
    Task PublishAsync(Wide2ProgressUpdate update, CancellationToken cancellationToken = default);
}

// Default no-op used when no real transport is registered (e.g. hosts without SignalR). Keeps the
// pipeline decoupled from the transport so grounding/scoring/readiness are completely unaffected.
public sealed class NullWide2ProgressPublisher : IWide2ProgressPublisher
{
    public static readonly NullWide2ProgressPublisher Instance = new();
    public Task PublishAsync(Wide2ProgressUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
