namespace Ams.Application.Common.Dtos;

public sealed class SubmissionDto
{
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public string SubmissionNumber { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal? TargetPremium { get; set; }
    public int MarketCount { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class SubmissionActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionLineDto
{
    public Guid SubmissionLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? OpportunityLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public decimal TargetPremium { get; set; }
    public string? Priority { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? TargetEffectiveDate { get; set; }
}

public sealed class SubmissionIntakeQuestionDto
{
    public Guid IntakeQuestionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? AnswerText { get; set; }
    public bool IsAnswered { get; set; }
    public Guid? AnsweredByUserId { get; set; }
    public DateTime? AnsweredDateUtc { get; set; }
}

public sealed class SubmissionDocumentChecklistDto
{
    public Guid ChecklistItemId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsSatisfied { get; set; }
    public Guid? DocumentId { get; set; }
    public string? FileName { get; set; }
    public DateTime? UploadedDateUtc { get; set; }
}

public sealed class SubmissionReadinessDto
{
    public Guid SubmissionId { get; set; }
    public int RequiredQuestionCount { get; set; }
    public int AnsweredRequiredQuestionCount { get; set; }
    public int RequiredDocumentCount { get; set; }
    public int SatisfiedRequiredDocumentCount { get; set; }
    public bool IsReadyForMarketing { get; set; }
    public IReadOnlyList<string> BlockingReasons { get; set; } = [];
}

public sealed class SubmissionTaskTemplateDto
{
    public string TaskTypeCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = "Medium";
    public int DefaultDueDays { get; set; }
}

public sealed class SubmissionMetricsDto
{
    public int PendingIntake { get; set; }
    public int ReadyForMarket { get; set; }
    public int MarketsAwaitingResponse { get; set; }
    public int QuotesExpiringSoon { get; set; }
    public int ProposalsPendingDecision { get; set; }
    public int BindRequestsPending { get; set; }
}

public sealed class SubmissionTaskDto
{
    public Guid TaskItemId { get; set; }
    public Guid TenantId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
