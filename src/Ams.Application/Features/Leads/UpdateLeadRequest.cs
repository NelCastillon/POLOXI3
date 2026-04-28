namespace Ams.Application.Features.Leads;

public sealed class UpdateLeadRequest
{
    public Guid LeadId { get; set; }
    public string? AccountName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? InterestedService { get; set; }
    public int? Score { get; set; }
    public string? PriorityCode { get; set; }
    public string? SourceCode { get; set; }
    public string? NurturingStageCode { get; set; }
    public DateTime? QualifiedDate { get; set; }
    public int? StatusCode { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
