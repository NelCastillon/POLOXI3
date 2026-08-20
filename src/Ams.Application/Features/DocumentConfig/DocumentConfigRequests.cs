namespace Ams.Application.Features.DocumentConfig;

public sealed record CreateDocumentConfigItemRequest(
    Guid TenantId,
    string Kind,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateDocumentConfigItemRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);

public sealed record CreateDocumentGroupRequest(
    Guid TenantId,
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    int SortOrder);

public sealed record UpdateDocumentGroupRequest(
    string Code,
    string Name,
    string? Category,
    string? Description,
    string? ConfigurationJson,
    bool IsActive,
    int SortOrder);
