using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentWorkflowTemplate : AuditableEntity
{
    public string TemplateName { get; private set; } = string.Empty;
    public string TemplateCode { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string WorkflowType { get; private set; } = string.Empty;
    public bool IsSequential { get; private set; } = true;
    public bool RequiresAllApprovals { get; private set; } = true;
    public bool AutoArchiveOnComplete { get; private set; }
    public bool NotifyOnStart { get; private set; } = true;
    public bool NotifyOnComplete { get; private set; } = true;
    public bool TriggerOnUpload { get; private set; }
    public string? TriggerOnCategory { get; private set; }
    public string? TriggerOnDocType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    private DocumentWorkflowTemplate() { }

    public DocumentWorkflowTemplate(
        Guid tenantId,
        string templateName,
        string templateCode,
        string workflowType,
        string? description,
        bool isSequential,
        bool requiresAllApprovals,
        bool autoArchiveOnComplete,
        bool notifyOnStart,
        bool notifyOnComplete,
        bool triggerOnUpload,
        string? triggerOnCategory,
        string? triggerOnDocType,
        int sortOrder,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        TemplateName = templateName;
        TemplateCode = templateCode;
        WorkflowType = workflowType;
        Description = description;
        IsSequential = isSequential;
        RequiresAllApprovals = requiresAllApprovals;
        AutoArchiveOnComplete = autoArchiveOnComplete;
        NotifyOnStart = notifyOnStart;
        NotifyOnComplete = notifyOnComplete;
        TriggerOnUpload = triggerOnUpload;
        TriggerOnCategory = triggerOnCategory;
        TriggerOnDocType = triggerOnDocType;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public void Update(
        string templateName,
        string? description,
        bool isSequential,
        bool requiresAllApprovals,
        bool autoArchiveOnComplete,
        bool notifyOnStart,
        bool notifyOnComplete,
        bool triggerOnUpload,
        string? triggerOnCategory,
        string? triggerOnDocType,
        int sortOrder,
        Guid? modifiedByUserId)
    {
        TemplateName = templateName;
        Description = description;
        IsSequential = isSequential;
        RequiresAllApprovals = requiresAllApprovals;
        AutoArchiveOnComplete = autoArchiveOnComplete;
        NotifyOnStart = notifyOnStart;
        NotifyOnComplete = notifyOnComplete;
        TriggerOnUpload = triggerOnUpload;
        TriggerOnCategory = triggerOnCategory;
        TriggerOnDocType = triggerOnDocType;
        SortOrder = sortOrder;
        MarkModified(modifiedByUserId);
    }

    public void Activate(Guid? modifiedByUserId)
    {
        IsActive = true;
        MarkModified(modifiedByUserId);
    }

    public void Deactivate(Guid? modifiedByUserId)
    {
        IsActive = false;
        MarkModified(modifiedByUserId);
    }
}
