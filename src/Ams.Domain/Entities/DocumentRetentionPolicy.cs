using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentRetentionPolicy : AuditableEntity
{
    public string PolicyName { get; private set; } = string.Empty;
    public string PolicyCode { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ApplicableCategory { get; private set; }
    public string? ApplicableDocType { get; private set; }
    public string? ApplicableEntityType { get; private set; }
    public int RetentionPeriodYears { get; private set; }
    public string RetentionStartTrigger { get; private set; } = "Creation";
    public string ActionOnExpiry { get; private set; } = "Archive";
    public bool RequireApprovalToDelete { get; private set; } = true;
    public int? NotifyBeforeDays { get; private set; }
    public string? NotifyRoleCode { get; private set; }
    public string? RegulatoryBasis { get; private set; }
    public string? ComplianceNotes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

    private DocumentRetentionPolicy() { }

    public DocumentRetentionPolicy(
        Guid tenantId,
        string policyName,
        string policyCode,
        string? description,
        string? applicableCategory,
        string? applicableDocType,
        string? applicableEntityType,
        int retentionPeriodYears,
        string retentionStartTrigger,
        string actionOnExpiry,
        bool requireApprovalToDelete,
        int? notifyBeforeDays,
        string? notifyRoleCode,
        string? regulatoryBasis,
        string? complianceNotes,
        DateOnly effectiveDate,
        DateOnly? expiryDate,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PolicyName = policyName;
        PolicyCode = policyCode;
        Description = description;
        ApplicableCategory = applicableCategory;
        ApplicableDocType = applicableDocType;
        ApplicableEntityType = applicableEntityType;
        RetentionPeriodYears = retentionPeriodYears;
        RetentionStartTrigger = retentionStartTrigger;
        ActionOnExpiry = actionOnExpiry;
        RequireApprovalToDelete = requireApprovalToDelete;
        NotifyBeforeDays = notifyBeforeDays;
        NotifyRoleCode = notifyRoleCode;
        RegulatoryBasis = regulatoryBasis;
        ComplianceNotes = complianceNotes;
        EffectiveDate = effectiveDate;
        ExpiryDate = expiryDate;
        IsActive = true;
    }

    public void Update(
        string policyName,
        string? description,
        string? applicableCategory,
        string? applicableDocType,
        string? applicableEntityType,
        int retentionPeriodYears,
        string retentionStartTrigger,
        string actionOnExpiry,
        bool requireApprovalToDelete,
        int? notifyBeforeDays,
        string? notifyRoleCode,
        string? regulatoryBasis,
        string? complianceNotes,
        DateOnly? expiryDate,
        Guid? modifiedByUserId)
    {
        PolicyName = policyName;
        Description = description;
        ApplicableCategory = applicableCategory;
        ApplicableDocType = applicableDocType;
        ApplicableEntityType = applicableEntityType;
        RetentionPeriodYears = retentionPeriodYears;
        RetentionStartTrigger = retentionStartTrigger;
        ActionOnExpiry = actionOnExpiry;
        RequireApprovalToDelete = requireApprovalToDelete;
        NotifyBeforeDays = notifyBeforeDays;
        NotifyRoleCode = notifyRoleCode;
        RegulatoryBasis = regulatoryBasis;
        ComplianceNotes = complianceNotes;
        ExpiryDate = expiryDate;
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
