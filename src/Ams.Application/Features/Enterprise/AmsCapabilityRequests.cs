namespace Ams.Application.Features.Enterprise;

public sealed record SearchAmsCapabilitiesRequest(
    Guid TenantId,
    string? DomainCode = null,
    string? StatusCode = null,
    string? PriorityCode = null,
    string? SearchTerm = null,
    bool ActiveOnly = true);

public sealed record UpdateAmsCapabilityRequest(
    string CurrentState,
    string StatusCode,
    string PriorityCode,
    int MaturityScore,
    string ExistingModuleRoute,
    string RecommendedAction,
    string DataSource,
    string? ConfigurationJson,
    bool IsActive);
