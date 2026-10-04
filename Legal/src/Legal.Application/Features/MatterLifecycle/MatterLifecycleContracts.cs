namespace Legal.Application.Features.MatterLifecycle;

// ── Judz Matter Lifecycle contracts ─────────────────────────────────────────
// DB-backed read models and requests for the lifecycle engine (POLOXI.Legal_Matter*
// tables from migrations 0385/0386). Lifecycle supplies OPERATIONAL STAGE CONTEXT
// only: it never owns POLOXI Core decision state. All stages/transitions/requirements
// are sourced from the database — never hardcoded here.

// A stage definition within a published lifecycle version.
public sealed record MatterLifecycleStageDto(
    Guid StageDefinitionId,
    string Code,
    string Name,
    string? StageCategory,
    int DisplayOrder,
    bool IsInitial,
    bool IsTerminal,
    bool AllowReentry,
    int? DefaultSlaDays);

// A directed transition edge available from the matter's current stage.
public sealed record MatterLifecycleTransitionDto(
    Guid TransitionDefinitionId,
    Guid FromStageDefinitionId,
    Guid ToStageDefinitionId,
    string ToStageCode,
    string ToStageName,
    string Code,
    string Name,
    string? TransitionType,
    bool RequiresApproval,
    int Priority);

// An immutable stage-occupancy record (newest first in snapshots).
public sealed record MatterLifecycleHistoryDto(
    Guid HistoryId,
    Guid StageDefinitionId,
    string StageCode,
    string StageName,
    DateTime EnteredUtc,
    DateTime? ExitedUtc,
    string? EntryReasonCode,
    string? ExitReasonCode,
    string? ChangedByType);

// Runtime satisfaction state of a stage requirement for the current stage.
public sealed record MatterLifecycleRequirementDto(
    Guid RequirementDefinitionId,
    string Code,
    string Name,
    string RequirementType,
    string RequirementLevel,
    bool IsBlocking,
    string StatusCode,
    bool IsSatisfied);

// An append-only operational event (powers "Since Your Last Review").
public sealed record MatterLifecycleEventDto(
    Guid EventId,
    string EventType,
    DateTime OccurredUtc,
    string Title,
    string? Description,
    string? StageCode);

// Composed lifecycle read model for one matter.
public sealed record MatterLifecycleSnapshotDto(
    Guid MatterLifecycleId,
    Guid DecisionMatterId,
    Guid LifecycleVersionId,
    string LifecycleDefinitionCode,
    string LifecycleDefinitionName,
    int VersionNumber,
    string StatusCode,
    string AuthorityMode,
    Guid CurrentStageDefinitionId,
    string CurrentStageCode,
    string CurrentStageName,
    DateTime CurrentStageEnteredUtc,
    IReadOnlyList<MatterLifecycleStageDto> Stages,
    IReadOnlyList<MatterLifecycleHistoryDto> History,
    IReadOnlyList<MatterLifecycleRequirementDto> CurrentStageRequirements,
    IReadOnlyList<MatterLifecycleTransitionDto> AvailableTransitions,
    IReadOnlyList<MatterLifecycleEventDto> RecentEvents);

// Ensures a matter has an active primary lifecycle. If none exists, one is created
// from the default (or specified) lifecycle for the matter's type, starting at the
// initial stage. AuthorityMode defaults to JUDZ_AUTHORITATIVE; Clio-sourced matters
// should pass EXTERNAL_AUTHORITATIVE so sync does not fight the attorney.
public sealed record EnsureMatterLifecycleRequest(
    Guid DecisionMatterId,
    string? LifecycleDefinitionCode = null,
    string? AuthorityMode = null,
    Guid? ExternalSystemId = null,
    string? ExternalStageCode = null);

// Performs a directed transition from the current stage to the target stage. The
// transition MUST exist as an active edge in the lifecycle graph (never inferred
// from display order). History is appended, never destroyed.
public sealed record PerformMatterLifecycleTransitionRequest(
    Guid DecisionMatterId,
    Guid TransitionDefinitionId,
    string? Notes = null);

// ── Configuration admin surface (DB-backed; managed from the Lifecycle config UI) ───────
// A lifecycle definition with its published/draft versions and stage/transition counts.
public sealed record MatterLifecycleDefinitionDto(
    Guid MatterLifecycleDefinitionId,
    string Code,
    string Name,
    string? Description,
    string? MatterTypeCode,
    string? JurisdictionCode,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    int VersionCount,
    int PublishedVersionCount);

// A version row under a definition, with its stage/transition counts.
public sealed record MatterLifecycleVersionDto(
    Guid MatterLifecycleVersionId,
    Guid MatterLifecycleDefinitionId,
    int VersionNumber,
    string? VersionLabel,
    string StatusCode,
    DateTime? PublishedUtc,
    int StageCount,
    int TransitionCount);

// A full definition detail used by the config editor (definition + versions + stages + transitions).
public sealed record MatterLifecycleDefinitionDetailDto(
    MatterLifecycleDefinitionDto Definition,
    IReadOnlyList<MatterLifecycleVersionDto> Versions,
    IReadOnlyList<MatterLifecycleStageDto> Stages,
    IReadOnlyList<MatterLifecycleTransitionDto> Transitions);

// Create/update a lifecycle definition. Code is immutable after create (identity).
public sealed record SaveMatterLifecycleDefinitionRequest(
    Guid? MatterLifecycleDefinitionId,
    string Code,
    string Name,
    string? Description,
    string? MatterTypeCode,
    string? JurisdictionCode,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    string? DomainPackCode = null);

// Create/update a stage within a version.
public sealed record SaveMatterLifecycleStageRequest(
    Guid? MatterStageDefinitionId,
    Guid MatterLifecycleVersionId,
    string Code,
    string Name,
    string? Description,
    string? StageCategory,
    int DisplayOrder,
    bool IsInitial,
    bool IsTerminal,
    bool AllowReentry,
    int? DefaultSlaDays,
    bool IsActive);

// Create a transition edge within a version.
public sealed record SaveMatterLifecycleTransitionRequest(
    Guid? MatterStageTransitionDefinitionId,
    Guid MatterLifecycleVersionId,
    Guid FromStageDefinitionId,
    Guid ToStageDefinitionId,
    string Code,
    string Name,
    string? TransitionType,
    bool RequiresApproval,
    int Priority,
    bool IsActive);
