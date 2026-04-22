namespace Ams.Application.Features.Lobs;

public sealed record CreateLineOfBusinessRequest(
    Guid    TenantId,
    string  LobCode,
    string  LobName,
    string  Category,
    string? Description);

public sealed record UpdateLineOfBusinessRequest(
    string  LobCode,
    string  LobName,
    string  Category,
    string? Description,
    bool    IsActive);
