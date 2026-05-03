namespace Ams.Application.Features.PolicyConfig;

// Coverage Types
public sealed record CreateCoverageTypeRequest(Guid TenantId, string CoverageCode, string CoverageName, string? LobCode, string? Description, int SortOrder);
public sealed record UpdateCoverageTypeRequest(string CoverageCode, string CoverageName, string? LobCode, string? Description, bool IsActive, int SortOrder);

// Policy Statuses
public sealed record CreatePolicyStatusRequest(Guid TenantId, string StatusCode, string StatusName, string? StatusType, string? Description, string? ColorHex, bool IsDefault, int SortOrder);
public sealed record UpdatePolicyStatusRequest(string StatusCode, string StatusName, string? StatusType, string? Description, string? ColorHex, bool IsDefault, bool IsActive, int SortOrder);

// Endorsement Types
public sealed record CreateEndorsementTypeRequest(Guid TenantId, string TypeCode, string TypeName, string? Description, int SortOrder);
public sealed record UpdateEndorsementTypeRequest(string TypeCode, string TypeName, string? Description, bool IsActive, int SortOrder);

// Cancellation Reasons
public sealed record CreateCancellationReasonRequest(Guid TenantId, string ReasonCode, string ReasonName, string? ReasonType, string? Description, int SortOrder);
public sealed record UpdateCancellationReasonRequest(string ReasonCode, string ReasonName, string? ReasonType, string? Description, bool IsActive, int SortOrder);

// Certificate Settings
public sealed record UpdateCertificateSettingRequest(string? SettingValue);

// ID Card Settings
public sealed record UpdateIdCardSettingRequest(string? SettingValue);

// Policy Custom Fields
public sealed record CreatePolicyCustomFieldRequest(Guid TenantId, string FieldCode, string FieldName, string EntityType, string FieldType, string? DefaultValue, string? DropdownOptions, bool IsRequired, bool IsSearchable, int SortOrder);
public sealed record UpdatePolicyCustomFieldRequest(string FieldCode, string FieldName, string EntityType, string FieldType, string? DefaultValue, string? DropdownOptions, bool IsRequired, bool IsSearchable, bool IsActive, int SortOrder);
