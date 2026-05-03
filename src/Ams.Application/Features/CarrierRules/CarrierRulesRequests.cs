namespace Ams.Application.Features.CarrierRules;

public sealed record CreateMarketAccessRuleRequest(Guid TenantId, string RuleName, string? CarrierNaic, string? StateCode, string? LobCode, string? AccessLevel, string? Requirements, int Priority);
public sealed record UpdateMarketAccessRuleRequest(string RuleName, string? CarrierNaic, string? StateCode, string? LobCode, string? AccessLevel, string? Requirements, int Priority, bool IsActive);

public sealed record CreateCarrierDownloadMappingRequest(Guid TenantId, string MappingCode, string? CarrierNaic, string? TransactionType, string? SourceField, string? TargetField, string? TransformRule, int SortOrder);
public sealed record UpdateCarrierDownloadMappingRequest(string MappingCode, string? CarrierNaic, string? TransactionType, string? SourceField, string? TargetField, string? TransformRule, bool IsActive, int SortOrder);
