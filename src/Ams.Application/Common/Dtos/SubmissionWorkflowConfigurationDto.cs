namespace Ams.Application.Common.Dtos;

public sealed class SubmissionIntakeTemplateDto
{
    public Guid IntakeTemplateId { get; set; }
    public Guid TenantId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string QuestionCode { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class SubmissionDocumentRequirementDto
{
    public Guid DocumentRequirementId { get; set; }
    public Guid TenantId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class SubmissionWorkflowConfigurationSummaryDto
{
    public int IntakeTemplateCount { get; set; }
    public int RequiredIntakeTemplateCount { get; set; }
    public int DocumentRequirementCount { get; set; }
    public int RequiredDocumentRequirementCount { get; set; }
    public int LineOfBusinessCount { get; set; }
}
