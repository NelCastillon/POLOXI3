namespace Ams.Domain.Entities;

public sealed class FieldChangeLog
{
    public Guid FieldChangeLogId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string FieldName { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public DateTime ChangedDateUtc { get; private set; } = DateTime.UtcNow;
    public string? ChangeSource { get; private set; }
    public string? IpAddress { get; private set; }
    public bool IsDeleted { get; private set; }

    private FieldChangeLog() { }

    public FieldChangeLog(Guid tenantId, string entityName, Guid entityId, string fieldName, string? oldValue, string? newValue)
    {
        TenantId = tenantId;
        EntityName = entityName;
        EntityId = entityId;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
