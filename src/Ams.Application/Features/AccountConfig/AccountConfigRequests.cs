namespace Ams.Application.Features.AccountConfig;

public sealed record CreateAccountTypeRequest(Guid TenantId, string TypeCode, string TypeName, string? Category, string? Description, bool IsDefault, int SortOrder);
public sealed record UpdateAccountTypeRequest(string TypeCode, string TypeName, string? Category, string? Description, bool IsDefault, bool IsActive, int SortOrder);

public sealed record CreateRelationshipTypeRequest(Guid TenantId, string TypeCode, string TypeName, bool IsBidirectional, string? InverseTypeCode, string? Description, int SortOrder);
public sealed record UpdateRelationshipTypeRequest(string TypeCode, string TypeName, bool IsBidirectional, string? InverseTypeCode, string? Description, bool IsActive, int SortOrder);

public sealed record UpdateHouseholdSettingRequest(string SettingValue);
public sealed record UpdateCommercialEntitySettingRequest(string SettingValue);

public sealed record CreateContactTypeRequest(Guid TenantId, string TypeCode, string TypeName, string? Description, bool IsDefault, int SortOrder);
public sealed record UpdateContactTypeRequest(string TypeCode, string TypeName, string? Description, bool IsDefault, bool IsActive, int SortOrder);

public sealed record CreateAccountCustomFieldRequest(Guid TenantId, string FieldCode, string FieldName, string EntityType, string FieldType, string? DefaultValue, string? DropdownOptions, bool IsRequired, bool IsSearchable, int SortOrder);
public sealed record UpdateAccountCustomFieldRequest(string FieldCode, string FieldName, string EntityType, string FieldType, string? DefaultValue, string? DropdownOptions, bool IsRequired, bool IsSearchable, bool IsActive, int SortOrder);
