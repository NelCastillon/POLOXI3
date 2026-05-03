namespace Ams.Application.Common.Dtos;

public sealed class CoverageTypeDto
{
    public Guid     CoverageTypeId { get; set; }
    public Guid     TenantId       { get; set; }
    public string   CoverageCode   { get; set; } = string.Empty;
    public string   CoverageName   { get; set; } = string.Empty;
    public string?  LobCode        { get; set; }
    public string?  Description    { get; set; }
    public bool     IsActive       { get; set; }
    public int      SortOrder      { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PolicyStatusDto
{
    public Guid     PolicyStatusId  { get; set; }
    public Guid     TenantId        { get; set; }
    public string   StatusCode      { get; set; } = string.Empty;
    public string   StatusName      { get; set; } = string.Empty;
    public string?  StatusType      { get; set; }
    public string?  Description     { get; set; }
    public string?  ColorHex        { get; set; }
    public bool     IsDefault       { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class EndorsementTypeDto
{
    public Guid     EndorsementTypeId { get; set; }
    public Guid     TenantId          { get; set; }
    public string   TypeCode          { get; set; } = string.Empty;
    public string   TypeName          { get; set; } = string.Empty;
    public string?  Description       { get; set; }
    public bool     IsActive          { get; set; }
    public int      SortOrder         { get; set; }
    public DateTime CreatedDateUtc    { get; set; }
}

public sealed class CancellationReasonDto
{
    public Guid     CancellationReasonId { get; set; }
    public Guid     TenantId             { get; set; }
    public string   ReasonCode           { get; set; } = string.Empty;
    public string   ReasonName           { get; set; } = string.Empty;
    public string?  ReasonType           { get; set; }
    public string?  Description          { get; set; }
    public bool     IsActive             { get; set; }
    public int      SortOrder            { get; set; }
    public DateTime CreatedDateUtc       { get; set; }
}

public sealed class CertificateSettingDto
{
    public Guid     CertificateSettingId { get; set; }
    public Guid     TenantId             { get; set; }
    public string   SettingKey           { get; set; } = string.Empty;
    public string?  SettingValue         { get; set; }
    public string?  SettingType          { get; set; }
    public string?  Description          { get; set; }
    public DateTime CreatedDateUtc       { get; set; }
}

public sealed class IdCardSettingDto
{
    public Guid     IdCardSettingId  { get; set; }
    public Guid     TenantId         { get; set; }
    public string   SettingKey       { get; set; } = string.Empty;
    public string?  SettingValue     { get; set; }
    public string?  SettingType      { get; set; }
    public string?  Description      { get; set; }
    public DateTime CreatedDateUtc   { get; set; }
}

public sealed class PolicyCustomFieldDto
{
    public Guid     CustomFieldId    { get; set; }
    public Guid     TenantId         { get; set; }
    public string   FieldCode        { get; set; } = string.Empty;
    public string   FieldName        { get; set; } = string.Empty;
    public string   EntityType       { get; set; } = string.Empty;
    public string   FieldType        { get; set; } = string.Empty;
    public string?  DefaultValue     { get; set; }
    public string?  DropdownOptions  { get; set; }
    public bool     IsRequired       { get; set; }
    public bool     IsSearchable     { get; set; }
    public bool     IsActive         { get; set; }
    public int      SortOrder        { get; set; }
    public DateTime CreatedDateUtc   { get; set; }
}
