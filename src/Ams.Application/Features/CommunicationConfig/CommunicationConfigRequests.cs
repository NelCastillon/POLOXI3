namespace Ams.Application.Features.CommunicationConfig;

public sealed record CreateCommunicationConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Channel,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateCommunicationConfigItemRequest(
    string Code,
    string Name,
    string? Channel,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
