namespace Ams.Application.Features.Appetite;

public sealed record CreateAppetiteRuleRequest(
    Guid    TenantId,
    string  RuleName,
    string  LobCode,
    string? CarrierNaic,
    string  RuleJson,
    string  AppetiteLevel,
    int     Priority);

public sealed record UpdateAppetiteRuleRequest(
    string  RuleName,
    string  LobCode,
    string? CarrierNaic,
    string  RuleJson,
    string  AppetiteLevel,
    int     Priority,
    bool    IsActive);
