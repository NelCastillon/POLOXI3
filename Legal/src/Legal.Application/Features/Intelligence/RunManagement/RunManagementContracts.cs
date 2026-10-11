namespace Legal.Application.Features.Intelligence.RunManagement;

// ── Run & Assessment Management read contracts. All values are DB-backed and tenant-scoped;
//    no hardcoded operational data is produced in C# or Razor. ──

// A single execution attempt within a run step.
public sealed record AnalysisRunAttemptDto(
    int AttemptNumber,
    string StatusCode,
    string? Summary,
    DateTime? StartedUtc,
    DateTime? CompletedUtc)
{
    // Display text, e.g. "Attempt 1: Timeout".
    public string DisplayText => Summary is { Length: > 0 }
        ? $"Attempt {AttemptNumber}: {Summary}"
        : $"Attempt {AttemptNumber}: {StatusCode}";
}

// One actual operation within the pipeline, with its retained attempts.
public sealed record AnalysisRunStepDto(
    Guid AnalysisRunStepId,
    string StepCode,
    string DisplayName,
    string? Description,
    int Sequence,
    string StatusCode,
    string? StatusLabel,
    IReadOnlyList<AnalysisRunAttemptDto> Attempts);

// A run in the ledger list (left column) and the detail panel source (right column).
public sealed record AnalysisRunDto(
    Guid AnalysisRunId,
    string RunLabel,
    string Title,
    string RunTypeCode,
    string? TriggerTypeCode,
    string? TriggerText,
    string ExecutionStatusCode,
    string PublicationStatusCode,
    string? PackLabel,
    string? InputSnapshotLabel,
    DateTime RequestedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    int? DurationSeconds,
    string? ExecutionBanner,
    string? FooterNote,
    IReadOnlyList<AnalysisRunStepDto> Steps)
{
    // The immutable input snapshot this run executed under (null for pre-snapshot historical runs).
    public AnalysisRunInputSnapshotDto? InputSnapshot { get; init; }
}

// The immutable input snapshot (S-###) a run executed under. Replayed verbatim in the UI so opening a
// historical run reproduces its recorded inputs even after the matter/prompt/Domain Pack later change.
public sealed record AnalysisRunInputSnapshotDto(
    Guid InputSnapshotId,
    Guid AnalysisRunId,
    string SnapshotLabel,
    string? Query,
    string? ContextCode,
    string? ModeCode,
    string? ModelCode,
    string? DomainPackCode,
    string? DiscoveryModeCode,
    string? Jurisdiction,
    string? GoverningLaw,
    string? CourtOrForum,
    string? Posture,
    string? MotionTarget,
    string? CounterfactualAssumption,
    string? ClarificationAnswer,
    bool? UsedDependencyGraph,
    string ContractJson,
    DateTime CapturedUtc);

// The guarded current-assessment pointer for the matter (top banner).
public sealed record CurrentAssessmentDto(
    Guid CurrentAssessmentId,
    string AssessmentLabel,
    Guid AnalysisRunId,
    string RunLabel,
    string? ReviewStateText,
    string? FreshnessStatusCode,
    string? FreshnessNote,
    DateTime PublishedUtc);

// The full page view model assembled by the service for a matter.
public sealed record RunAssessmentPageDto(
    Guid MatterId,
    string MatterTitle,
    string? MatterReference,
    string? MatterStage,
    CurrentAssessmentDto? CurrentAssessment,
    IReadOnlyList<AnalysisRunDto> Runs);
