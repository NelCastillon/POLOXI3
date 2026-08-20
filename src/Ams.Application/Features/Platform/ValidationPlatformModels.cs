using System.Text.Json;

namespace Ams.Application.Features.Platform;

public sealed record ExecuteValidationsRequest(
    Guid TenantId,
    string EntityTypeCode,
    Guid EntityId,
    string? ModuleCode,
    string? JurisdictionCode,
    Guid? CarrierId,
    string? LineOfBusinessCode,
    string CorrelationId,
    JsonElement Facts,
    Guid? ActorUserId);

public sealed record ValidationResultDto(
    Guid ValidationResultId,
    Guid ValidationDefinitionId,
    string ValidationCode,
    string StatusCode,
    string SeverityCode,
    bool IsBlocking,
    bool CanBeWaived,
    string? WaiverPermissionCode,
    string Message,
    JsonElement Evidence);

public sealed record ValidationExecutionResponse(
    Guid ValidationExecutionId,
    string CorrelationId,
    string StatusCode,
    bool IsValid,
    bool HasBlockingFailures,
    IReadOnlyCollection<ValidationResultDto> Results);
