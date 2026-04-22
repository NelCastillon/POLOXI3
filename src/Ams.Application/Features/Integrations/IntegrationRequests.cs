namespace Ams.Application.Features.Integrations;

public sealed record CreateWebhookEndpointRequest(
    Guid TenantId,
    string Name,
    string TargetUrl,
    string[] EventTypes,
    string? Secret = null);

public sealed record UpdateWebhookEndpointRequest(
    string Name,
    string TargetUrl,
    string[] EventTypes,
    bool IsActive);

public sealed record CreateAutomationFlowRequest(
    Guid TenantId,
    string Name,
    string Description,
    string TriggerType);

public sealed record UpdateAutomationFlowRequest(
    string Name,
    string Description,
    string TriggerType,
    bool IsActive);

public sealed record SaveWorkflowDesignRequest(
    Guid TenantId,
    string Name,
    string Version,
    string DiagramJson);

public sealed record ResolveDownloadExceptionRequest(
    Guid ResolvedByUserId,
    string ResolutionNote);
