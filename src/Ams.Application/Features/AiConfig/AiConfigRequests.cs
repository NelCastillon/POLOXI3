namespace Ams.Application.Features.AiConfig;

public sealed record CreateAiConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateAiConfigItemRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
