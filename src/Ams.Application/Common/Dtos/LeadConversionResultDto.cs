namespace Ams.Application.Common.Dtos;

public sealed class LeadConversionResultDto
{
    public Guid LeadConversionId { get; set; }
    public Guid LeadId { get; set; }
    public Guid AccountId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid? ContactId { get; set; }
    public string AccountActionCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string OpportunityName { get; set; } = string.Empty;
    public string? OpportunityNumber { get; set; }
    public string? LineOfBusiness { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public bool SubmissionDraftRequested { get; set; }
    public Guid? SubmissionId { get; set; }
    public string? SubmissionNumber { get; set; }
    public bool SubmissionDraftCreated { get; set; }
}
