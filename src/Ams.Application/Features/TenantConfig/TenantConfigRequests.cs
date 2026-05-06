namespace Ams.Application.Features.TenantConfig;

public sealed record CreateTenantConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateTenantConfigItemRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
