namespace Ams.Application.Features.DataConfig;

public sealed record CreateDataConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateDataConfigItemRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
