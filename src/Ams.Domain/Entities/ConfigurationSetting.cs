namespace Ams.Domain.Entities;

public sealed class ConfigurationSetting
{
    public Guid SettingId { get; private set; } = Guid.NewGuid();
    public Guid? TenantId { get; private set; }
    public string ScopeCode { get; private set; } = "Tenant";
    public Guid? ScopeEntityId { get; private set; }
    public string SettingKey { get; private set; } = string.Empty;
    public string? SettingValue { get; private set; }
    public string DataTypeCode { get; private set; } = "String";
    public string? DefaultValue { get; private set; }
    public string? Description { get; private set; }
    public bool IsEncrypted { get; private set; }
    public bool IsReadOnly { get; private set; }
    public string? ModuleCode { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid? ModifiedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private ConfigurationSetting() { }

    public ConfigurationSetting(string settingKey, string? settingValue, string scopeCode, string dataTypeCode)
    {
        SettingKey = settingKey;
        SettingValue = settingValue;
        ScopeCode = scopeCode;
        DataTypeCode = dataTypeCode;
    }
}
