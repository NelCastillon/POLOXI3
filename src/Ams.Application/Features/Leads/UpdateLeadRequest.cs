using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Leads;

public sealed class UpdateLeadRequest
{
    public Guid LeadId { get; set; }
    [StringLength(200)]
    public string? AccountName { get; set; }
    [StringLength(100)]
    public string? FirstName { get; set; }
    [StringLength(100)]
    public string? LastName { get; set; }
    [AmsEmailAddress, StringLength(200)]
    public string? Email { get; set; }
    [AmsPhone]
    [StringLength(30)]
    public string? Phone { get; set; }
    [StringLength(1000)]
    public string? InterestedService { get; set; }
    [Range(0, 999999999999)]
    public decimal? AnnualRevenue { get; set; }
    [Range(0, 100)]
    public int? Score { get; set; }
    [StringLength(50)]
    public string? PriorityCode { get; set; }
    [StringLength(50)]
    public string? SourceCode { get; set; }
    [StringLength(50)]
    public string? NurturingStageCode { get; set; }
    public DateTime? QualifiedDate { get; set; }
    public int? StatusCode { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
