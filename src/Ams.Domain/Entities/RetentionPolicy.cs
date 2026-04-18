using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class RetentionPolicy : AuditableEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public int RetentionDays { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;
    public string? Description { get; private set; }
    public DateTime? LastAppliedDateUtc { get; private set; }
    public int? LastAppliedCount { get; private set; }

    private RetentionPolicy() { }

    public RetentionPolicy(Guid tenantId, string entityName, int retentionDays, string actionCode, string? description, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        EntityName = entityName;
        RetentionDays = retentionDays;
        ActionCode = actionCode;
        Description = description;
    }

    public void Update(int retentionDays, string actionCode, bool isEnabled, string? description, Guid? modifiedByUserId)
    {
        RetentionDays = retentionDays;
        ActionCode = actionCode;
        IsEnabled = isEnabled;
        Description = description;
        MarkModified(modifiedByUserId);
    }

    public void RecordApplication(int affectedCount)
    {
        LastAppliedDateUtc = DateTime.UtcNow;
        LastAppliedCount = affectedCount;
    }
}
