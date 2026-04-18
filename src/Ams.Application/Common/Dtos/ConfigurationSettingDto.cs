namespace Ams.Application.Common.Dtos;

public sealed class ConfigurationSettingDto
{
    public Guid SettingId { get; set; }
    public Guid? TenantId { get; set; }
    public string ScopeCode { get; set; } = string.Empty;
    public Guid? ScopeEntityId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string DataTypeCode { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsReadOnly { get; set; }
    public string? ModuleCode { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
