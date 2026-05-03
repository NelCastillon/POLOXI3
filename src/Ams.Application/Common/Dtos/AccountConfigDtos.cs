namespace Ams.Application.Common.Dtos;

public sealed class AccountTypeDto
{
    public Guid     AccountTypeId   { get; set; }
    public Guid     TenantId        { get; set; }
    public string   TypeCode        { get; set; } = string.Empty;
    public string   TypeName        { get; set; } = string.Empty;
    public string?  Category        { get; set; }
    public string?  Description     { get; set; }
    public bool     IsDefault       { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class RelationshipTypeDto
{
    public Guid     RelationshipTypeId { get; set; }
    public Guid     TenantId           { get; set; }
    public string   TypeCode           { get; set; } = string.Empty;
    public string   TypeName           { get; set; } = string.Empty;
    public bool     IsBidirectional    { get; set; }
    public string?  InverseTypeCode    { get; set; }
    public string?  Description        { get; set; }
    public bool     IsActive           { get; set; }
    public int      SortOrder          { get; set; }
    public DateTime CreatedDateUtc     { get; set; }
}

public sealed class HouseholdSettingDto
{
    public Guid     HouseholdSettingId { get; set; }
    public Guid     TenantId           { get; set; }
    public string   SettingKey         { get; set; } = string.Empty;
    public string?  SettingValue       { get; set; }
    public string?  SettingType        { get; set; }
    public string?  Description        { get; set; }
    public DateTime CreatedDateUtc     { get; set; }
}

public sealed class CommercialEntitySettingDto
{
    public Guid     CommercialEntitySettingId { get; set; }
    public Guid     TenantId                  { get; set; }
    public string   SettingKey                { get; set; } = string.Empty;
    public string?  SettingValue              { get; set; }
    public string?  SettingType               { get; set; }
    public string?  Description               { get; set; }
    public DateTime CreatedDateUtc            { get; set; }
}

public sealed class ContactTypeDto
{
    public Guid     ContactTypeId   { get; set; }
    public Guid     TenantId        { get; set; }
    public string   TypeCode        { get; set; } = string.Empty;
    public string   TypeName        { get; set; } = string.Empty;
    public string?  Description     { get; set; }
    public bool     IsDefault       { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class AccountCustomFieldDto
{
    public Guid     CustomFieldId   { get; set; }
    public Guid     TenantId        { get; set; }
    public string   FieldCode       { get; set; } = string.Empty;
    public string   FieldName       { get; set; } = string.Empty;
    public string   EntityType      { get; set; } = string.Empty;
    public string   FieldType       { get; set; } = string.Empty;
    public string?  DefaultValue    { get; set; }
    public string?  DropdownOptions { get; set; }
    public bool     IsRequired      { get; set; }
    public bool     IsSearchable    { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}
