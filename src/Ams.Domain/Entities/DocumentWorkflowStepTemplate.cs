using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentWorkflowStepTemplate : AuditableEntity
{
    public Guid WorkflowTemplateId { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public string StepType { get; private set; } = string.Empty;
    public int StepOrder { get; private set; }
    public string? Description { get; private set; }
    public string? AssignedToRoleCode { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public bool AssignToBranchAdmin { get; private set; }
    public bool AssignToDocOwner { get; private set; }
    public bool IsRequired { get; private set; } = true;
    public int? DueDays { get; private set; }
    public int? EscalateDays { get; private set; }
    public string? EscalateToRoleCode { get; private set; }
    public bool RequiresPreviousApproval { get; private set; }
    public string? SkipIfCondition { get; private set; }

    private DocumentWorkflowStepTemplate() { }

    public DocumentWorkflowStepTemplate(
        Guid tenantId,
        Guid workflowTemplateId,
        string stepName,
        string stepType,
        int stepOrder,
        string? description,
        string? assignedToRoleCode,
        Guid? assignedToUserId,
        bool assignToBranchAdmin,
        bool assignToDocOwner,
        bool isRequired,
        int? dueDays,
        int? escalateDays,
        string? escalateToRoleCode,
        bool requiresPreviousApproval,
        string? skipIfCondition,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        WorkflowTemplateId = workflowTemplateId;
        StepName = stepName;
        StepType = stepType;
        StepOrder = stepOrder;
        Description = description;
        AssignedToRoleCode = assignedToRoleCode;
        AssignedToUserId = assignedToUserId;
        AssignToBranchAdmin = assignToBranchAdmin;
        AssignToDocOwner = assignToDocOwner;
        IsRequired = isRequired;
        DueDays = dueDays;
        EscalateDays = escalateDays;
        EscalateToRoleCode = escalateToRoleCode;
        RequiresPreviousApproval = requiresPreviousApproval;
        SkipIfCondition = skipIfCondition;
    }

    public void Update(
        string stepName,
        string? description,
        string? assignedToRoleCode,
        Guid? assignedToUserId,
        bool assignToBranchAdmin,
        bool assignToDocOwner,
        bool isRequired,
        int? dueDays,
        int? escalateDays,
        string? escalateToRoleCode,
        bool requiresPreviousApproval,
        string? skipIfCondition,
        Guid? modifiedByUserId)
    {
        StepName = stepName;
        Description = description;
        AssignedToRoleCode = assignedToRoleCode;
        AssignedToUserId = assignedToUserId;
        AssignToBranchAdmin = assignToBranchAdmin;
        AssignToDocOwner = assignToDocOwner;
        IsRequired = isRequired;
        DueDays = dueDays;
        EscalateDays = escalateDays;
        EscalateToRoleCode = escalateToRoleCode;
        RequiresPreviousApproval = requiresPreviousApproval;
        SkipIfCondition = skipIfCondition;
        MarkModified(modifiedByUserId);
    }
}
