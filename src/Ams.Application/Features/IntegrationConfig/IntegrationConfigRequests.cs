namespace Ams.Application.Features.IntegrationConfig;

public sealed record CreateIntegrationConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateIntegrationConfigItemRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
